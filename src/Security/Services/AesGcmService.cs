using System.Security.Cryptography;
using Security.Interfaces;

namespace Security.Services;

public class AesGcmService : IAesGcmService
{
    private const int KeySizeBytes = 32; // 256 bits
    private const int NonceSizeBytes = 12; // 96 bits standard for GCM
    private const int TagSizeBytes = 16; // 128 bits

    public byte[] GenerateRandomKey256()
    {
        return RandomNumberGenerator.GetBytes(KeySizeBytes);
    }

    public (byte[] CipherText, byte[] Nonce, byte[] Tag) Encrypt(byte[] plainBytes, byte[] key256)
    {
        if (plainBytes == null) throw new ArgumentNullException(nameof(plainBytes));
        if (key256 == null || key256.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes (256-bit).", nameof(key256));

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] cipherText = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(key256, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
        }

        return (cipherText, nonce, tag);
    }

    public byte[] Decrypt(byte[] cipherText, byte[] key256, byte[] nonce, byte[] tag)
    {
        if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));
        if (key256 == null || key256.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes (256-bit).", nameof(key256));
        if (nonce == null || nonce.Length != NonceSizeBytes)
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes.", nameof(nonce));
        if (tag == null || tag.Length != TagSizeBytes)
            throw new ArgumentException($"Tag must be {TagSizeBytes} bytes.", nameof(tag));

        byte[] plainBytes = new byte[cipherText.Length];

        using (var aesGcm = new AesGcm(key256, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);
        }

        return plainBytes;
    }
}
