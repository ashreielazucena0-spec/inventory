using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// PBKDF2 password hashing in the same text layout already stored in
    /// tbl_account:  iterations.base64(salt).base64(hash)
    /// New passwords use 100000 iterations, 16-byte salt, 32-byte SHA-256 hash.
    /// Verify() also accepts a SHA-1 PBKDF2 hash of the same layout, and
    /// legacy plain-text passwords (which are upgraded after a successful login).
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private static readonly Regex HashFormat =
            new Regex(@"^\d{4,7}\.[A-Za-z0-9+/]+={0,2}\.[A-Za-z0-9+/]+={0,2}$", RegexOptions.Compiled);

        public static bool IsHashed(string stored)
        {
            return !string.IsNullOrEmpty(stored) && HashFormat.IsMatch(stored);
        }

        public static bool NeedsUpgrade(string stored)
        {
            return !IsHashed(stored);
        }

        public static string Hash(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash = Derive(password, salt, Iterations, HashSize, HashAlgorithmName.SHA256);
            return Iterations + "." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            if (password == null || stored == null) return false;

            if (!IsHashed(stored))
                return string.Equals(password, stored, StringComparison.Ordinal); // legacy plain text

            try
            {
                string[] parts = stored.Split('.');
                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);

                if (FixedTimeEquals(expected, Derive(password, salt, iterations, expected.Length, HashAlgorithmName.SHA256)))
                    return true;
                return FixedTimeEquals(expected, Derive(password, salt, iterations, expected.Length, HashAlgorithmName.SHA1));
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length, HashAlgorithmName alg)
        {
            var kdf = new Rfc2898DeriveBytes(password, salt, iterations, alg);
            return kdf.GetBytes(length);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
