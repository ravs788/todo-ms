using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace AuthService.Services
{
    /// <summary>
    /// Contract for password hashing and verification.
    /// </summary>
    public interface IPasswordService
    {
        /// <summary>
        /// Hashes a plaintext password using PBKDF2-HMACSHA256 with random salt.
        /// </summary>
        /// <param name="password">Plaintext password.</param>
        /// <returns>Encoded hash string including version, iterations, salt, and subkey.</returns>
        string Hash(string password);

        /// <summary>
        /// Verifies a plaintext password against a previously generated hash.
        /// </summary>
        /// <param name="password">Plaintext password.</param>
        /// <param name="encodedHash">Encoded hash string.</param>
        /// <returns>True if valid; otherwise false.</returns>
        bool Verify(string password, string encodedHash);
    }

    /// <summary>
    /// PBKDF2 (HMACSHA256) implementation with salt and iteration count.
    /// Encoded format: v1.{iterations}.{saltBase64}.{subkeyBase64}
    /// </summary>
    public sealed class PasswordService : IPasswordService
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int KeySize = 32;

        public string Hash(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var subkey = KeyDerivation.Pbkdf2(
                password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                Iterations,
                KeySize
            );

            return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
        }

        public bool Verify(string password, string encodedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encodedHash))
                return false;

            var parts = encodedHash.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !parts[0].Equals("v1", StringComparison.Ordinal))
                return false;

            if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
                return false;

            byte[] salt, expectedSubkey;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expectedSubkey = Convert.FromBase64String(parts[3]);
            }
            catch
            {
                return false;
            }

            var actualSubkey = KeyDerivation.Pbkdf2(
                password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                iterations,
                expectedSubkey.Length
            );

            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }
    }
}
