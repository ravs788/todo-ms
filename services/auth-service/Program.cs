using AuthService.Models;
using AuthService.Services;
using AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthService;

/// <summary>
/// Application entrypoint and composition root for Auth Service.
/// </summary>
public class Program
{
    /// <summary>
    /// Bootstraps web host, configures DI and minimal API endpoints.
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind basic JWT options (uses defaults if section missing)
        var jwtOptions = new JwtOptions();
        builder.Configuration.GetSection("Jwt").Bind(jwtOptions);

        // Bind to all interfaces so Kong (in Docker) can reach host service via host.docker.internal
        builder.WebHost.UseUrls("http://0.0.0.0:5001");

        // Minimal services
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Persistence
        builder.Services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDatabase")));

        // DI for JWT issuing and JWKS
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddSingleton<JwtKeyProvider>();
        builder.Services.AddSingleton<IJwtService, JwtService>();
        builder.Services.AddScoped<IPasswordService, PasswordService>();

        var app = builder.Build();

        // Ensure database is created (dev thin slice)
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.Database.EnsureCreated();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // JWKS endpoint (public keys for token verification)
        app.MapGet("/api/v1/auth/jwks", (IJwtService jwt) =>
        {
            return Results.Json(jwt.GetJwks());
        })
        .WithName("GetJwks")
        .WithOpenApi();

        // Register new user (hash password, persist, issue tokens)
        app.MapPost("/api/v1/auth/register", (AuthDbContext db, IPasswordService pwd, IJwtService jwt, JwtOptions options, RegisterRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { message = "Username and password are required" });
            }

            var exists = db.Users.Any(u => u.Username == req.Username);
            if (exists)
            {
                return Results.Conflict(new { message = "Username already exists" });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = req.Username,
                PasswordHash = pwd.Hash(req.Password)
            };
            db.Users.Add(user);

            var roles = new[] { "USER" };
            var access = jwt.IssueAccessToken(user.Id.ToString("N"), user.Username, roles);
            var refresh = jwt.IssueRefreshToken();
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refresh,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            });

            db.SaveChanges();
            var response = new LoginResponse(access, refresh, options.AccessTokenMinutes * 60);
            return Results.Ok(response);
        })
        .WithName("Register")
        .WithOpenApi();

        // Login: validate user credentials, issue tokens, persist refresh
        app.MapPost("/api/v1/auth/login", (AuthDbContext db, IPasswordService pwd, IJwtService jwt, JwtOptions options, LoginRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { message = "Username and password are required" });
            }

            var user = db.Users.FirstOrDefault(u => u.Username == req.Username);
            if (user == null || !pwd.Verify(req.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var roles = new[] { "USER" };
            var access = jwt.IssueAccessToken(user.Id.ToString("N"), user.Username, roles);
            var refresh = jwt.IssueRefreshToken();
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refresh,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            });

            db.SaveChanges();
            var response = new LoginResponse(access, refresh, options.AccessTokenMinutes * 60);
            return Results.Ok(response);
        })
        .WithName("Login")
        .WithOpenApi();

        // Liveness health endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("Health")
            .WithOpenApi();

        // Namespaced health endpoint (accessible via gateway prefix)
        app.MapGet("/api/v1/auth/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("AuthHealth")
            .WithOpenApi();

        // Refresh: validate and rotate refresh token, issue new access
        app.MapPost("/api/v1/auth/refresh", (AuthDbContext db, IJwtService jwt, JwtOptions options, RefreshRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                return Results.BadRequest(new { message = "Refresh token is required" });
            }

            var rt = db.RefreshTokens.FirstOrDefault(t => t.Token == req.RefreshToken);
            if (rt == null || !rt.IsActive)
            {
                return Results.Unauthorized();
            }

            var user = db.Users.FirstOrDefault(u => u.Id == rt.UserId);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            // Revoke old and rotate
            rt.RevokedAt = DateTime.UtcNow;

            var newRefresh = jwt.IssueRefreshToken();
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newRefresh,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            });

            var roles = new[] { "USER" };
            var access = jwt.IssueAccessToken(user.Id.ToString("N"), user.Username, roles);

            db.SaveChanges();

            var response = new LoginResponse(access, newRefresh, options.AccessTokenMinutes * 60);
            return Results.Ok(response);
        })
        .WithName("RefreshToken")
        .WithOpenApi();

        // Logout: revoke a refresh token
        app.MapPost("/api/v1/auth/logout", (AuthDbContext db, LogoutRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                return Results.BadRequest(new { message = "Refresh token is required" });
            }

            var rt = db.RefreshTokens.FirstOrDefault(t => t.Token == req.RefreshToken);
            if (rt != null && rt.RevokedAt == null)
            {
                rt.RevokedAt = DateTime.UtcNow;
                db.SaveChanges();
            }

            return Results.Ok(new { success = true });
        })
        .WithName("Logout")
        .WithOpenApi();

        app.Run();
    }
}
