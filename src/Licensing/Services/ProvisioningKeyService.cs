using System.Text;
using System.Text.Json;
using Domain.Models;
using Security.Constants;
using Security.Interfaces;

namespace Licensing.Services;

public interface IProvisioningKeyService
{
    string GenerateProvisioningDataKey(ClientProvisioningPayload payload, string caPrivateKeyPem);
    (bool Success, string ErrorMessage, ClientProvisioningPayload? Payload) UnpackAndValidateDataKey(string keyContent, string? caPublicKeyPem = null);
    string GenerateLicenseRenewal(LicenseRenewalPayload payload, string caPrivateKeyPem);
    (bool Success, string ErrorMessage, LicenseRenewalPayload? Payload) ValidateLicenseRenewal(string licenseContent, string? caPublicKeyPem = null);
}

public class ProvisioningKeyService : IProvisioningKeyService
{
    private readonly IAesGcmService _aesGcm;
    private readonly IRsaCryptoService _rsa;

    public ProvisioningKeyService(IAesGcmService aesGcm, IRsaCryptoService rsa)
    {
        _aesGcm = aesGcm;
        _rsa = rsa;
    }

    public string GenerateProvisioningDataKey(ClientProvisioningPayload payload, string caPrivateKeyPem)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(caPrivateKeyPem)) throw new ArgumentException("CA Private Key is required.", nameof(caPrivateKeyPem));

        string json = JsonSerializer.Serialize(payload);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(json);

        byte[] aesKey = _aesGcm.GenerateRandomKey256();
        var (cipherText, nonce, tag) = _aesGcm.Encrypt(payloadBytes, aesKey);

        // Sign the payload cipher + metadata with CA Private Key
        byte[] dataToSign = CombineBytes(cipherText, nonce, tag, Encoding.UTF8.GetBytes(payload.BusinessCode));
        byte[] signature = _rsa.SignData(dataToSign, caPrivateKeyPem);

        var envelope = new EncryptedPackageEnvelope
        {
            PackageType = SecurityConstants.DataKeyPackageType,
            SchemaVersion = "1.0",
            BusinessCode = payload.BusinessCode,
            TimestampUtc = DateTime.UtcNow,
            EncryptedPayloadBase64 = Convert.ToBase64String(cipherText),
            NonceBase64 = Convert.ToBase64String(nonce),
            TagBase64 = Convert.ToBase64String(tag),
            WrappedKeyBase64 = Convert.ToBase64String(aesKey), // In local offline model, AES key is stored in signed envelope
            RsaSignatureBase64 = Convert.ToBase64String(signature)
        };

        string envelopeJson = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(envelopeJson));
    }

    public (bool Success, string ErrorMessage, ClientProvisioningPayload? Payload) UnpackAndValidateDataKey(string keyContent, string? caPublicKeyPem = null)
    {
        if (string.IsNullOrWhiteSpace(keyContent))
            return (false, "Data key content is empty.", null);

        try
        {
            string publicKey = caPublicKeyPem ?? SecurityConstants.EmbeddedCaPublicKeyPem;
            byte[] envelopeBytes = Convert.FromBase64String(keyContent.Trim());
            string envelopeJson = Encoding.UTF8.GetString(envelopeBytes);

            var envelope = JsonSerializer.Deserialize<EncryptedPackageEnvelope>(envelopeJson);
            if (envelope == null || envelope.PackageType != SecurityConstants.DataKeyPackageType)
                return (false, "Invalid package format or package type mismatch.", null);

            byte[] cipherText = Convert.FromBase64String(envelope.EncryptedPayloadBase64);
            byte[] nonce = Convert.FromBase64String(envelope.NonceBase64);
            byte[] tag = Convert.FromBase64String(envelope.TagBase64);
            byte[] aesKey = Convert.FromBase64String(envelope.WrappedKeyBase64);
            byte[] signature = Convert.FromBase64String(envelope.RsaSignatureBase64);

            // 1. Verify CA RSA Signature
            byte[] signedData = CombineBytes(cipherText, nonce, tag, Encoding.UTF8.GetBytes(envelope.BusinessCode));
            bool isSignatureValid = _rsa.VerifyData(signedData, signature, publicKey);
            if (!isSignatureValid)
                return (false, "Cryptographic signature validation failed! This key has been tampered with or was not issued by the Super Admin CA.", null);

            // 2. Decrypt Payload
            byte[] plainBytes = _aesGcm.Decrypt(cipherText, aesKey, nonce, tag);
            string json = Encoding.UTF8.GetString(plainBytes);
            var payload = JsonSerializer.Deserialize<ClientProvisioningPayload>(json);

            if (payload == null)
                return (false, "Failed to parse decrypted business provisioning payload.", null);

            return (true, string.Empty, payload);
        }
        catch (Exception ex)
        {
            return (false, $"Decryption / validation error: {ex.Message}", null);
        }
    }

    public string GenerateLicenseRenewal(LicenseRenewalPayload payload, string caPrivateKeyPem)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(caPrivateKeyPem)) throw new ArgumentException("CA Private Key is required.", nameof(caPrivateKeyPem));

        string dataForSignature = $"{payload.LicenseId}|{payload.BusinessCode}|{payload.Plan}|{payload.IssuedDateUtc:O}|{payload.ExpiryDateUtc:O}|{payload.MaxUsers}|{payload.MaxProducts}";
        byte[] dataBytes = Encoding.UTF8.GetBytes(dataForSignature);
        byte[] signature = _rsa.SignData(dataBytes, caPrivateKeyPem);
        payload.Signature = Convert.ToBase64String(signature);

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public (bool Success, string ErrorMessage, LicenseRenewalPayload? Payload) ValidateLicenseRenewal(string licenseContent, string? caPublicKeyPem = null)
    {
        if (string.IsNullOrWhiteSpace(licenseContent))
            return (false, "License content is empty.", null);

        try
        {
            string publicKey = caPublicKeyPem ?? SecurityConstants.EmbeddedCaPublicKeyPem;
            byte[] jsonBytes = Convert.FromBase64String(licenseContent.Trim());
            string json = Encoding.UTF8.GetString(jsonBytes);

            var payload = JsonSerializer.Deserialize<LicenseRenewalPayload>(json);
            if (payload == null || string.IsNullOrEmpty(payload.Signature))
                return (false, "Invalid license payload or missing signature.", null);

            string dataForSignature = $"{payload.LicenseId}|{payload.BusinessCode}|{payload.Plan}|{payload.IssuedDateUtc:O}|{payload.ExpiryDateUtc:O}|{payload.MaxUsers}|{payload.MaxProducts}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataForSignature);
            byte[] signature = Convert.FromBase64String(payload.Signature);

            bool isValid = _rsa.VerifyData(dataBytes, signature, publicKey);
            if (!isValid)
                return (false, "Digital signature verification failed on license key.", null);

            return (true, string.Empty, payload);
        }
        catch (Exception ex)
        {
            return (false, $"License parsing error: {ex.Message}", null);
        }
    }

    private static byte[] CombineBytes(byte[] a, byte[] b, byte[] c, byte[] d)
    {
        byte[] result = new byte[a.Length + b.Length + c.Length + d.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        Buffer.BlockCopy(c, 0, result, a.Length + b.Length, c.Length);
        Buffer.BlockCopy(d, 0, result, a.Length + b.Length + c.Length, d.Length);
        return result;
    }
}
