using System;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using AuthService;
using AuthService.Data;
using AuthService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AuthService.Tests
{
    public class PasswordServiceTests
    {
        [Fact]
        public void Hash_And_Verify_Succeeds()
        {
            var svc = new PasswordService();
            var hash = svc.Hash("secret123");
            hash.Should().NotBeNullOrWhiteSpace();

            svc.Verify("secret123", hash).Should().BeTrue();
        }

        [Fact]
        public void Verify_WrongPassword_Fails()
        {
            var svc = new PasswordService();
            var hash = svc.Hash("right-password");

            svc.Verify("wrong-password", hash).Should().BeFalse();
        }

        [Fact]
        public void Verify_BadHash_ReturnsFalse()
        {
            var svc = new PasswordService();
            svc.Verify("anything", "not-a-valid-hash").Should().BeFalse();
        }
    }

    public class JwtServiceValidateTests
    {
        [Fact]
        public void ValidateToken_Returns_Principal_For_Valid_Token_And_Null_For_Invalid()
        {
            var options = new JwtOptions
            {
                Issuer = "todo-ms-auth",
                Audience = "todo-ms-clients",
                AccessTokenMinutes = 15
            };
            var keyProvider = new JwtKeyProvider();
            var sut = new JwtService(options, keyProvider);

            var token = sut.IssueAccessToken("sub-123", "alice", new[] { "USER" });

            var principal = sut.ValidateToken(token);
            principal.Should().NotBeNull();
            principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("sub-123");

            // Tamper token (append char) to force invalid signature
            var tampered = token + "x";
            sut.ValidateToken(tampered).Should().BeNull();
        }
    }

    public class RefreshTokenModelTests
    {
        [Fact]
        public void IsActive_True_If_NotRevoked_And_NotExpired()
        {
            var rt = new RefreshToken
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                RevokedAt = null
            };
            rt.IsActive.Should().BeTrue();
        }

        [Fact]
        public void IsActive_False_If_Revoked()
        {
            var rt = new RefreshToken
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                RevokedAt = DateTime.UtcNow
            };
            rt.IsActive.Should().BeFalse();
        }

        [Fact]
        public void IsActive_False_If_Expired()
        {
            var rt = new RefreshToken
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                RevokedAt = null
            };
            rt.IsActive.Should().BeFalse();
        }
    }

    // Negative/edge-case endpoint coverage using in-memory TestServer from TestWebApplicationFactory
    public class AuthNegativeEndpointsTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public AuthNegativeEndpointsTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Register_MissingUsername_Returns_400()
        {
            var client = _factory.CreateClient();
            var payload = new { username = "", password = "pwd" };

            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_DuplicateUsername_Returns_409()
        {
            var client = _factory.CreateClient();
            var name = $"dup_{Guid.NewGuid():N}";
            var payload = new { username = name, password = "pwd" };

            var first = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
            first.EnsureSuccessStatusCode();

            var second = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
            second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Login_MissingFields_Returns_400()
        {
            var client = _factory.CreateClient();
            var payload = new { username = "", password = "" };

            var resp = await client.PostAsJsonAsync("/api/v1/auth/login", payload);
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_WrongPassword_Returns_401()
        {
            var client = _factory.CreateClient();
            var name = $"user_{Guid.NewGuid():N}";
            var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new { username = name, password = "right" });
            reg.EnsureSuccessStatusCode();

            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = name, password = "wrong" });
            login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Refresh_MissingToken_Returns_400()
        {
            var client = _factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "" });
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_InvalidToken_Returns_401()
        {
            var client = _factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "not-a-valid-token" });
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Logout_MissingToken_Returns_400()
        {
            var client = _factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "" });
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
