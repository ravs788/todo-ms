using AuthService.Models;
using AuthService.Services;

namespace AuthService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind basic JWT options (uses defaults if section missing)
        var jwtOptions = new JwtOptions();
        builder.Configuration.GetSection("Jwt").Bind(jwtOptions);

        // Force HTTP on :5001 for local dev to match gateway expectations
        builder.WebHost.UseUrls("http://localhost:5001");

        // Minimal services
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // DI for JWT issuing and JWKS
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddSingleton<JwtKeyProvider>();
        builder.Services.AddSingleton<IJwtService, JwtService>();

        var app = builder.Build();

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

        // Minimal login (dev): accept any non-empty username/password and issue RS256 JWT
        app.MapPost("/api/v1/auth/login", (IJwtService jwt, JwtOptions options, LoginRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { message = "Username and password are required" });
            }

            var subject = Guid.NewGuid().ToString("N");
            var roles = new[] { "USER" };
            var token = jwt.IssueAccessToken(subject, req.Username, roles);

            var response = new LoginResponse(token, options.AccessTokenMinutes * 60);
            return Results.Ok(response);
        })
        .WithName("Login")
        .WithOpenApi();

        app.Run();
    }
}
