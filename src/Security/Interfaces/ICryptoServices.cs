namespace Security.Interfaces;

public interface IPasswordHasher
{
    (string HashBase64, string SaltBase64) HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hashBase64, string saltBase64);
}

public interface IAesGcmService
{
    (byte[] CipherText, byte[] Nonce, byte[] Tag) Encrypt(byte[] plainBytes, byte[] key256);
    byte[] Decrypt(byte[] cipherText, byte[] key256, byte[] nonce, byte[] tag);
    byte[] GenerateRandomKey256();
}

public interface IRsaCryptoService
{
    byte[] SignData(byte[] dataBytes, string privateKeyPem);
    bool VerifyData(byte[] dataBytes, byte[] signatureBytes, string publicKeyPem);
    byte[] EncryptKey(byte[] symmetricKey, string publicKeyPem);
    byte[] DecryptKey(byte[] encryptedKey, string privateKeyPem);
    (string PrivateKeyPem, string PublicKeyPem) GenerateKeyPair(int keySize = 4096);
}
