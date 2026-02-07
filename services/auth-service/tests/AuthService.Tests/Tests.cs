using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AuthService;
using AuthService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AuthService.Tests
{
    public class JwtServiceTests
    {
        [Fact]
        public void IssueAccessToken_Produces_RS256_JWT_With_Expected_Claims_And_Kid()
        {
            // Arrange
            var options = new JwtOptions
            {
                Issuer = "todo-ms-auth",
                Audience = "todo-ms-clients",
                AccessTokenMinutes = 15
            };
            var keyProvider = new JwtKeyProvider();
            var sut = new JwtService(options, keyProvider);

            // Act
            var tokenString = sut.IssueAccessToken("user-sub-123", "alice", new[] { "USER" });

            // Assert
            tokenString.Should().NotBeNullOrWhiteSpace();

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            token.Header.Alg.Should().Be("RS256");
            token.Header.ContainsKey("kid").Should().BeTrue();
            token.Header["kid"].Should().Be(keyProvider.GetKeyId());

            token.Issuer.Should().Be(options.Issuer);
            token.Audiences.Should().Contain(options.Audience);

            token.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "user-sub-123").Should().BeTrue();
            token.Claims.Any(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "alice").Should().BeTrue();
            token.Claims.Any(c => c.Type == "approved" && c.Value == "true").Should().BeTrue();
            token.Claims.Where(c => c.Type == "roles").Select(c => c.Value).Should().Contain("USER");

            // Expiration roughly within configured window
            var now = DateTime.UtcNow;
            token.ValidTo.Should().BeAfter(now.AddMinutes(13)); // some buffer for test runtime
            token.ValidTo.Should().BeBefore(now.AddMinutes(16));
        }
    }

    public class AuthEndpointsTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public AuthEndpointsTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Jwks_Endpoint_Returns_Keys_Array()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var resp = await client.GetAsync("/api/v1/auth/jwks");

            // Assert
            resp.EnsureSuccessStatusCode();
            var text = await resp.Content.ReadAsStringAsync();
            text.Should().Contain("\"keys\"");
        }

        [Fact]
        public async Task Login_Endpoint_Returns_AccessToken()
        {
            // Arrange
            var client = _factory.CreateClient();

            var payload = new
            {
                username = "alice",
                password = "pwd"
            };

            // Ensure user exists
            var reg = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
            reg.EnsureSuccessStatusCode();

            // Act
            var resp = await client.PostAsJsonAsync("/api/v1/auth/login", payload);

            // Assert
            resp.EnsureSuccessStatusCode();
            using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);
            doc.RootElement.TryGetProperty("accessToken", out var tokenProp).Should().BeTrue();
            tokenProp.GetString().Should().NotBeNullOrWhiteSpace();

            doc.RootElement.TryGetProperty("refreshToken", out var refProp).Should().BeTrue();
            refProp.GetString().Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Health_Endpoint_Returns_Healthy_Status()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var resp = await client.GetAsync("/health");

            // Assert
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("\"status\"");
            json.Should().Contain("\"healthy\"");
        }

        [Fact]
        public async Task AuthHealth_Endpoint_Returns_Healthy_Status()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var resp = await client.GetAsync("/api/v1/auth/health");

            // Assert
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("\"status\"");
            json.Should().Contain("\"healthy\"");
        }

        [Fact]
        public async Task Refresh_Endpoint_Returns_New_Tokens()
        {
            // Arrange
            var client = _factory.CreateClient();

            var creds = new { username = "bob", password = "pwd" };
            var reg = await client.PostAsJsonAsync("/api/v1/auth/register", creds);
            reg.EnsureSuccessStatusCode();

            var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", creds);
            loginResp.EnsureSuccessStatusCode();
            var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
            var refresh = loginJson.GetProperty("refreshToken").GetString();

            var refreshPayload = new { refreshToken = refresh };

            // Act
            var resp = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshPayload);

            // Assert
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("\"accessToken\"");
            json.Should().Contain("\"refreshToken\"");
        }

        [Fact]
        public async Task Register_Endpoint_Creates_User_And_Returns_Tokens()
        {
            var client = _factory.CreateClient();
            var payload = new { username = $"user_{Guid.NewGuid():N}", password = "pwd" };

            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", payload);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("\"accessToken\"");
            json.Should().Contain("\"refreshToken\"");
        }

        [Fact]
        public async Task Logout_Endpoint_Revokes_Refresh_Token()
        {
            var client = _factory.CreateClient();
            var u = new { username = $"user_{Guid.NewGuid():N}", password = "pwd" };

            // Register + login
            var reg2 = await client.PostAsJsonAsync("/api/v1/auth/register", u);
            reg2.EnsureSuccessStatusCode();
            var login = await client.PostAsJsonAsync("/api/v1/auth/login", u);
            login.EnsureSuccessStatusCode();
            var loginJson2 = await login.Content.ReadFromJsonAsync<JsonElement>();
            var refresh = loginJson2.GetProperty("refreshToken").GetString();

            // Logout (revoke)
            var logoutPayload = new { refreshToken = refresh };
            var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", logoutPayload);
            logout.EnsureSuccessStatusCode();

            // Attempt refresh with revoked token should be unauthorized
            var refreshPayload = new { refreshToken = refresh };
            var refreshResp = await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshPayload);
            ((int)refreshResp.StatusCode).Should().Be(401);
        }
    }
}
