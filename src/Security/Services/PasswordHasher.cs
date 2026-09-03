using System.Security.Cryptography;
using Security.Interfaces;

namespace Security.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltByteLength = 16;
    private const int HashByteLength = 64; // SHA-512 length
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

    public (string HashBase64, string SaltBase64) HashPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("Password cannot be empty.", nameof(plainPassword));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltByteLength);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            plainPassword,
            salt,
            Iterations,
            HashAlgorithm,
            HashByteLength);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string plainPassword, string hashBase64, string saltBase64)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64))
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            byte[] expectedHash = Convert.FromBase64String(hashBase64);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                plainPassword,
                salt,
                Iterations,
                HashAlgorithm,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
