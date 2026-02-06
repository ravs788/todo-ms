using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace AuthService.Services
{
    public sealed class JwtKeyProvider
    {
        private readonly RsaSecurityKey _rsaKey;
        private readonly string _kid;
        private readonly JsonWebKey _jwk;

        public JwtKeyProvider()
        {
            using var rsa = RSA.Create(2048);
            var parameters = rsa.ExportParameters(true);
            _rsaKey = new RsaSecurityKey(parameters);
            _kid = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16));
            _rsaKey.KeyId = _kid;

            // Build a JWK (public only) for JWKS exposure
            var rsaPublic = RSA.Create();
            rsaPublic.ImportParameters(new RSAParameters
            {
                Exponent = parameters.Exponent,
                Modulus = parameters.Modulus
            });

            var pubJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsaPublic));
            pubJwk.Kid = _kid;
            pubJwk.Alg = SecurityAlgorithms.RsaSha256;
            pubJwk.Kty = "RSA";
            pubJwk.Use = "sig";
            _jwk = pubJwk;
        }

        public SecurityKey GetSigningKey() => _rsaKey;

        public string GetKeyId() => _kid;

        public object GetJwksDocument()
        {
            // Return minimal JWKS document: {"keys":[ {jwk} ]}
            // Only public fields are included by JsonWebKey
            return new
            {
                keys = new[] { _jwk }
            };
        }
    }
}
