using Application.Services;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Persistence;
using Licensing.Services;
using Security.Interfaces;
using Security.Services;
using Xunit;

namespace UnitTests;

public class ProvisioningAndLicensingTests
{
    private readonly IAesGcmService _aesGcm = new AesGcmService();
    private readonly IRsaCryptoService _rsa = new RsaCryptoService();
    private readonly IPasswordHasher _hasher = new PasswordHasher();
    private readonly IProvisioningKeyService _keyService;
    private readonly ILicenseManager _licenseManager = new LicenseManager();

    public ProvisioningAndLicensingTests()
    {
        _keyService = new ProvisioningKeyService(_aesGcm, _rsa);
    }

    [Fact]
    public async Task ProvisioningFlow_FromCAGeneration_ToBusinessBootstrap_ShouldSucceed()
    {
        // 1. Setup CA Key Pair
        var (caPrivKey, caPubKey) = _rsa.GenerateKeyPair(2048);

        // 2. CA creates Client Business Profile & Key Request
        var clientPayload = new ClientProvisioningPayload
        {
            BusinessCode = "BUS-2026-001",
            LegalName = "Apex Retail Supermarket Ltd",
            TradeName = "Apex SuperMart",
            GSTIN = "27AABCA1234F1Z9",
            PAN = "AABCA1234F",
            BusinessType = BusinessType.Supermarket,
            Address = "123 Main Commercial Street",
            City = "Mumbai",
            State = "Maharashtra",
            Pincode = "400001",
            ContactPhone = "+91 9876543210",
            ContactEmail = "admin@apexsupermart.com",
            AdminUsername = "business_admin",
            AdminFullName = "Mahetab Admin",
            AdminTempPasswordPlain = "ApexSecure2026!",
            SubscriptionPlan = SubscriptionTier.Premium,
            IssuedDateUtc = DateTime.UtcNow,
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(3),
            MaxUsers = 10,
            MaxProducts = 50000
        };

        // 3. CA Generates Encrypted Data Key (.key file content)
        string dataKeyContent = _keyService.GenerateProvisioningDataKey(clientPayload, caPrivKey);
        dataKeyContent.Should().NotBeNullOrWhiteSpace();

        // 4. Business Admin receives data key & uploads to Business App
        var (unpackSuccess, unpackError, unpackedPayload) = _keyService.UnpackAndValidateDataKey(dataKeyContent, caPubKey);

        unpackSuccess.Should().BeTrue();
        unpackError.Should().BeEmpty();
        unpackedPayload.Should().NotBeNull();
        unpackedPayload!.BusinessCode.Should().Be("BUS-2026-001");
        unpackedPayload.LegalName.Should().Be("Apex Retail Supermarket Ltd");

        // 5. Business App Bootstraps local Database & Admin account
        var companyRepo = new LocalCompanyRepository();
        var userRepo = new LocalUserRepository();
        var licenseRepo = new LocalLicenseRepository();
        var auditRepo = new LocalAuditLogRepository();
        var bootstrapService = new BootstrapService(companyRepo, userRepo, licenseRepo, auditRepo, _hasher);

        var (bootSuccess, bootMsg) = await bootstrapService.BootstrapFromDataKeyAsync(unpackedPayload);
        bootSuccess.Should().BeTrue(bootMsg);

        // 6. Verify Initialized Database State
        bool isInit = await companyRepo.IsCompanyInitializedAsync();
        isInit.Should().BeTrue();

        var company = await companyRepo.GetCompanyAsync();
        company.Should().NotBeNull();
        company!.LegalName.Should().Be("Apex Retail Supermarket Ltd");
        company.BusinessType.Should().Be(BusinessType.Supermarket);

        var adminUser = await userRepo.GetByUsernameAsync("business_admin");
        adminUser.Should().NotBeNull();
        adminUser!.Role.Should().Be(UserRole.BusinessAdmin);
        _hasher.VerifyPassword("ApexSecure2026!", adminUser.PasswordHash, adminUser.Salt).Should().BeTrue();

        var license = await licenseRepo.GetCurrentLicenseAsync();
        license.Should().NotBeNull();
        license!.Status.Should().Be(LicenseStatus.Active);
        license.Plan.Should().Be(SubscriptionTier.Premium);

        var logs = await auditRepo.GetRecentLogsAsync();
        logs.Should().Contain(l => l.Action == AuditActionType.DataKeyUploaded);
    }

    [Fact]
    public void DataKey_WithTamperedPayload_ShouldBeRejected()
    {
        var (caPrivKey, caPubKey) = _rsa.GenerateKeyPair(2048);

        var clientPayload = new ClientProvisioningPayload
        {
            BusinessCode = "BUS-TAMPER-001",
            LegalName = "Legit Business",
            AdminUsername = "admin",
            AdminTempPasswordPlain = "Pass123"
        };

        string dataKeyBase64 = _keyService.GenerateProvisioningDataKey(clientPayload, caPrivKey);

        // Corrupt the base64 string
        char[] chars = dataKeyBase64.ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';
        string tamperedKey = new string(chars);

        var (success, error, _) = _keyService.UnpackAndValidateDataKey(tamperedKey, caPubKey);
        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [Fact]
    public void LicenseManager_ShouldCorrectlyIdentify_ExpiringSoon_AndGracePeriod()
    {
        var now = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

        var activeLicense = new Domain.Entities.LicenseRecord
        {
            LicenseKey = "LIC-001",
            IssuedDateUtc = now.AddDays(-10),
            ExpiryDateUtc = now.AddDays(20),
            GracePeriodDays = 7,
            Status = LicenseStatus.Active
        };

        var expiringSoonLicense = new Domain.Entities.LicenseRecord
        {
            LicenseKey = "LIC-002",
            IssuedDateUtc = now.AddDays(-25),
            ExpiryDateUtc = now.AddDays(4), // 4 days left
            GracePeriodDays = 7,
            Status = LicenseStatus.Active
        };

        var gracePeriodLicense = new Domain.Entities.LicenseRecord
        {
            LicenseKey = "LIC-003",
            IssuedDateUtc = now.AddDays(-35),
            ExpiryDateUtc = now.AddDays(-2), // 2 days past expiry
            GracePeriodDays = 7,
            Status = LicenseStatus.Active
        };

        var expiredLicense = new Domain.Entities.LicenseRecord
        {
            LicenseKey = "LIC-004",
            IssuedDateUtc = now.AddDays(-50),
            ExpiryDateUtc = now.AddDays(-10), // 10 days past expiry (> grace)
            GracePeriodDays = 7,
            Status = LicenseStatus.Active
        };

        _licenseManager.EvaluateLicenseStatus(activeLicense, now).Should().Be(LicenseStatus.Active);
        _licenseManager.EvaluateLicenseStatus(expiringSoonLicense, now).Should().Be(LicenseStatus.ExpiringSoon);
        _licenseManager.EvaluateLicenseStatus(gracePeriodLicense, now).Should().Be(LicenseStatus.InGracePeriod);
        _licenseManager.EvaluateLicenseStatus(expiredLicense, now).Should().Be(LicenseStatus.Expired);

        _licenseManager.CanPerformBilling(activeLicense, now).Should().BeTrue();
        _licenseManager.CanPerformBilling(expiringSoonLicense, now).Should().BeTrue();
        _licenseManager.CanPerformBilling(gracePeriodLicense, now).Should().BeTrue();
        _licenseManager.CanPerformBilling(expiredLicense, now).Should().BeFalse();
    }

    [Fact]
    public void CommercialAuditPackage_EncryptionAndDecryption_ShouldSucceed()
    {
        string masterKeyString = "AFS_MASTER_COMMERCIAL_KEY_2026!!";
        byte[] masterKey = System.Text.Encoding.UTF8.GetBytes(masterKeyString);

        string samplePayloadJson = """
        {
            "ExportMetadata": {
                "BusinessCode": "BUS-MUM-1001",
                "LegalName": "Metro HyperMarket Pvt Ltd",
                "GSTIN": "27AABCM1122F1Z4",
                "AccountingPeriod": "2026-09",
                "TotalInvoices": 15,
                "TotalRevenue": 45600.00,
                "TaxableTurnover": 38644.07,
                "TotalTax": 6955.93
            },
            "Invoices": [],
            "Products": [],
            "AuditLogs": []
        }
        """;

        byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(samplePayloadJson);

        // 1. DesktopApp Encrypts package
        var (cipherBytes, nonceBytes, tagBytes) = _aesGcm.Encrypt(plainBytes, masterKey);

        cipherBytes.Should().NotBeNullOrEmpty();
        nonceBytes.Length.Should().Be(12);
        tagBytes.Length.Should().Be(16);

        // 2. CA Super Admin Decrypts package
        byte[] decryptedBytes = _aesGcm.Decrypt(cipherBytes, masterKey, nonceBytes, tagBytes);
        string decryptedJson = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        decryptedJson.Should().Be(samplePayloadJson);
    }

    [Fact]
    public void LicenseRenewal_SigningAndValidation_ShouldSucceed()
    {
        var (caPrivKey, caPubKey) = _rsa.GenerateKeyPair(2048);

        var payload = new LicenseRenewalPayload
        {
            LicenseId = Guid.NewGuid().ToString("N"),
            BusinessCode = "BUS-2026-999",
            Plan = SubscriptionTier.Enterprise,
            IssuedDateUtc = DateTime.UtcNow,
            ExpiryDateUtc = DateTime.UtcNow.AddYears(1),
            MaxUsers = 50,
            MaxProducts = 100000
        };

        string renewalLicBase64 = _keyService.GenerateLicenseRenewal(payload, caPrivKey);
        renewalLicBase64.Should().NotBeNullOrWhiteSpace();

        var (success, error, verifiedPayload) = _keyService.ValidateLicenseRenewal(renewalLicBase64, caPubKey);
        success.Should().BeTrue();
        error.Should().BeEmpty();
        verifiedPayload.Should().NotBeNull();
        verifiedPayload!.BusinessCode.Should().Be("BUS-2026-999");
        verifiedPayload.Plan.Should().Be(SubscriptionTier.Enterprise);
    }
}
