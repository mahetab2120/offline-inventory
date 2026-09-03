using FluentAssertions;
using Security.Interfaces;
using Security.Services;
using Xunit;

namespace UnitTests;

public class CryptographyTests
{
    private readonly IPasswordHasher _passwordHasher = new PasswordHasher();
    private readonly IAesGcmService _aesGcm = new AesGcmService();
    private readonly IRsaCryptoService _rsa = new RsaCryptoService();

    [Fact]
    public void PasswordHasher_ShouldGenerateUniqueSalt_AndVerifyCorrectPassword()
    {
        string password = "SuperSecretPassword123!";
        var (hash1, salt1) = _passwordHasher.HashPassword(password);
        var (hash2, salt2) = _passwordHasher.HashPassword(password);

        salt1.Should().NotBe(salt2, "Salts must be cryptographically random and unique");
        hash1.Should().NotBe(hash2, "Hashes must differ because salts differ");

        bool isValid1 = _passwordHasher.VerifyPassword(password, hash1, salt1);
        bool isValid2 = _passwordHasher.VerifyPassword(password, hash2, salt2);
        bool isInvalid = _passwordHasher.VerifyPassword("WrongPassword", hash1, salt1);

        isValid1.Should().BeTrue();
        isValid2.Should().BeTrue();
        isInvalid.Should().BeFalse();
    }

    [Fact]
    public void AesGcmService_ShouldEncryptAndDecrypt_Successfully()
    {
        byte[] key = _aesGcm.GenerateRandomKey256();
        byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes("Confidential Invoice and GST Data Payload");

        var (cipherText, nonce, tag) = _aesGcm.Encrypt(plainBytes, key);

        cipherText.Should().NotBeNullOrEmpty();
        cipherText.Should().NotEqual(plainBytes);

        byte[] decrypted = _aesGcm.Decrypt(cipherText, key, nonce, tag);
        string decryptedText = System.Text.Encoding.UTF8.GetString(decrypted);

        decryptedText.Should().Be("Confidential Invoice and GST Data Payload");
    }

    [Fact]
    public void AesGcmService_ShouldFail_WhenCipherIsTampered()
    {
        byte[] key = _aesGcm.GenerateRandomKey256();
        byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes("Sensitive Data");

        var (cipherText, nonce, tag) = _aesGcm.Encrypt(plainBytes, key);
        cipherText[0] ^= 0xFF; // Flip bits to simulate tampering

        Action act = () => _aesGcm.Decrypt(cipherText, key, nonce, tag);
        act.Should().Throw<System.Security.Cryptography.AuthenticationTagMismatchException>();
    }

    [Fact]
    public void RsaCryptoService_ShouldSignAndVerify_Successfully()
    {
        var (privKey, pubKey) = _rsa.GenerateKeyPair(2048);
        byte[] data = System.Text.Encoding.UTF8.GetBytes("License BUS-10001 Valid until 2026-12-31");

        byte[] signature = _rsa.SignData(data, privKey);
        bool isVerified = _rsa.VerifyData(data, signature, pubKey);

        isVerified.Should().BeTrue();

        // Tampered data check
        byte[] tamperedData = System.Text.Encoding.UTF8.GetBytes("License BUS-10001 Valid until 2099-12-31");
        bool isTamperVerified = _rsa.VerifyData(tamperedData, signature, pubKey);
        isTamperVerified.Should().BeFalse();
    }
}
