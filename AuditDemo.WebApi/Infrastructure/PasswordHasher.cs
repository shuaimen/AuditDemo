using System;
using System.Security.Cryptography;

namespace AuditDemo.WebApi.Infrastructure
{
    public static class PasswordHasher
    {
        private const int Iterations = 10000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static void CreateHash(string password, out byte[] salt, out byte[] hash)
        {
            if (password == null) password = string.Empty;
            using (var rng = new RNGCryptoServiceProvider())
            {
                salt = new byte[SaltSize];
                rng.GetBytes(salt);
            }
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                hash = pbkdf2.GetBytes(HashSize);
            }
        }

        public static bool Verify(string password, byte[] salt, byte[] expectedHash)
        {
            if (password == null) password = string.Empty;
            if (salt == null || expectedHash == null) return false;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                var actual = pbkdf2.GetBytes(expectedHash.Length);
                return FixedTimeEquals(actual, expectedHash);
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
