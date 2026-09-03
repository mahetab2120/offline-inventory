using System.Security.Cryptography;
using System.Text;
using Security.Interfaces;

namespace Security.Services;

public class RsaCryptoService : IRsaCryptoService
{
    public (string PrivateKeyPem, string PublicKeyPem) GenerateKeyPair(int keySize = 4096)
    {
        using var rsa = RSA.Create(keySize);
        string privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        string publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        return (privateKeyPem, publicKeyPem);
    }

    public byte[] SignData(byte[] dataBytes, string privateKeyPem)
    {
        if (dataBytes == null) throw new ArgumentNullException(nameof(dataBytes));
        if (string.IsNullOrWhiteSpace(privateKeyPem)) throw new ArgumentException("Private key PEM cannot be empty.", nameof(privateKeyPem));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public bool VerifyData(byte[] dataBytes, byte[] signatureBytes, string publicKeyPem)
    {
        if (dataBytes == null || signatureBytes == null || string.IsNullOrWhiteSpace(publicKeyPem))
            return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    public byte[] EncryptKey(byte[] symmetricKey, string publicKeyPem)
    {
        if (symmetricKey == null) throw new ArgumentNullException(nameof(symmetricKey));
        if (string.IsNullOrWhiteSpace(publicKeyPem)) throw new ArgumentException("Public key PEM cannot be empty.", nameof(publicKeyPem));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return rsa.Encrypt(symmetricKey, RSAEncryptionPadding.OaepSHA256);
    }

    public byte[] DecryptKey(byte[] encryptedKey, string privateKeyPem)
    {
        if (encryptedKey == null) throw new ArgumentNullException(nameof(encryptedKey));
        if (string.IsNullOrWhiteSpace(privateKeyPem)) throw new ArgumentException("Private key PEM cannot be empty.", nameof(privateKeyPem));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
    }
}
