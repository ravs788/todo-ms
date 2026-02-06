using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services
{
    public interface IJwtService
    {
        string IssueAccessToken(string subject, string username, IEnumerable<string>? roles = null);
        object GetJwks();
    }

    public sealed class JwtService : IJwtService
    {
        private readonly JwtSecurityTokenHandler _handler = new();
        private readonly JwtOptions _options;
        private readonly JwtKeyProvider _keyProvider;

        public JwtService(JwtOptions options, JwtKeyProvider keyProvider)
        {
            _options = options;
            _keyProvider = keyProvider;
        }

        public string IssueAccessToken(string subject, string username, IEnumerable<string>? roles = null)
        {
            var now = DateTime.UtcNow;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(JwtRegisteredClaimNames.UniqueName, username),
                new("approved", "true")
            };

            if (roles != null)
            {
                foreach (var r in roles)
                {
                    claims.Add(new Claim("roles", r));
                }
            }

            var signingCredentials = new SigningCredentials(
                _keyProvider.GetSigningKey(),
                SecurityAlgorithms.RsaSha256
            );

            // add kid header
            var header = new JwtHeader(signingCredentials);
            header["kid"] = _keyProvider.GetKeyId();

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(_options.AccessTokenMinutes),
                signingCredentials: signingCredentials
            );

            token.Header["kid"] = _keyProvider.GetKeyId();

            return _handler.WriteToken(token);
        }

        public object GetJwks() => _keyProvider.GetJwksDocument();
    }
}
