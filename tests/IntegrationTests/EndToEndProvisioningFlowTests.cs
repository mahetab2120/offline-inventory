using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Persistence;
using Licensing.Services;
using Security.Constants;
using Security.Interfaces;
using Security.Services;
using Xunit;

namespace IntegrationTests;

public class EndToEndProvisioningFlowTests
{
    private readonly IAesGcmService _aesGcm = new AesGcmService();
    private readonly IRsaCryptoService _rsa = new RsaCryptoService();
    private readonly IPasswordHasher _hasher = new PasswordHasher();
    private readonly IProvisioningKeyService _keyService;
    private readonly ILicenseManager _licenseManager = new LicenseManager();

    public EndToEndProvisioningFlowTests()
    {
        _keyService = new ProvisioningKeyService(_aesGcm, _rsa);
    }

    [Fact]
    public async Task FullSystemLifecycle_FromCAKeyExport_ToPOSBilling_ShouldSucceed()
    {
        // =========================================================================
        // STEP 1: SUPER ADMIN (AFS) CREATES CLIENT & ISSUES ENCRYPTED DATA KEY
        // =========================================================================
        string caPrivKey = SecurityConstants.MasterCaPrivateKeyPem;

        var (adminHash, adminSalt) = _hasher.HashPassword("StoreAdmin@2026");

        var clientPayload = new ClientProvisioningPayload
        {
            BusinessCode = "BUS-MUMBAI-01",
            LegalName = "Mumbai Mega Retailers Pvt Ltd",
            TradeName = "Mega SuperMart",
            GSTIN = "27AAACM1234F1Z5",
            PAN = "AAACM1234F",
            BusinessType = BusinessType.Supermarket,
            Address = "Plot 42, Central Commercial Avenue",
            City = "Mumbai",
            State = "Maharashtra",
            Pincode = "400050",
            ContactPhone = "+91 9123456780",
            ContactEmail = "contact@megasupermart.com",
            AdminUsername = "business_admin",
            AdminFullName = "Store General Manager",
            AdminPasswordHash = adminHash,
            AdminSalt = adminSalt,
            AdminTempPasswordPlain = "StoreAdmin@2026",
            SubscriptionPlan = SubscriptionTier.Enterprise,
            IssuedDateUtc = DateTime.UtcNow,
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(12),
            GracePeriodDays = 7,
            MaxUsers = 50,
            MaxBranches = 5,
            MaxProducts = 100000,
            EnabledModules = new List<string> { "Billing", "Inventory", "GSTReports", "RackManagement" }
        };

        // Export .key file
        string dataKeyFileContent = _keyService.GenerateProvisioningDataKey(clientPayload, caPrivKey);
        dataKeyFileContent.Should().NotBeNullOrWhiteSpace();

        // =========================================================================
        // STEP 2: CLIENT BUSINESS APP FIRST RUN (LOCKED / UNINITIALIZED STATE)
        // =========================================================================
        var companyRepo = new LocalCompanyRepository();
        var userRepo = new LocalUserRepository();
        var licenseRepo = new LocalLicenseRepository();
        var auditRepo = new LocalAuditLogRepository();
        var productRepo = new LocalProductRepository();
        var invoiceRepo = new LocalInvoiceRepository();

        var bootstrapService = new BootstrapService(companyRepo, userRepo, licenseRepo, auditRepo, _hasher);
        var authService = new AuthenticationService(userRepo, _hasher, auditRepo);

        // Verify initial state is uninitialized
        (await companyRepo.IsCompanyInitializedAsync()).Should().BeFalse();

        // =========================================================================
        // STEP 3: BUSINESS ADMIN UPLOADS ENCRYPTED DATA KEY
        // =========================================================================
        var (unpackSuccess, unpackError, unpacked) = _keyService.UnpackAndValidateDataKey(dataKeyFileContent);
        unpackSuccess.Should().BeTrue();
        unpackError.Should().BeEmpty();
        unpacked.Should().NotBeNull();
        unpacked!.BusinessCode.Should().Be("BUS-MUMBAI-01");

        // Bootstrap Database
        var (bootSuccess, bootMsg) = await bootstrapService.BootstrapFromDataKeyAsync(unpacked);
        bootSuccess.Should().BeTrue();

        // Verify Initialized Status
        (await companyRepo.IsCompanyInitializedAsync()).Should().BeTrue();
        var company = await companyRepo.GetCompanyAsync();
        company!.LegalName.Should().Be("Mumbai Mega Retailers Pvt Ltd");
        company.TradeName.Should().Be("Mega SuperMart");

        // =========================================================================
        // STEP 4: BUSINESS ADMIN AUTHENTICATION
        // =========================================================================
        var (loginSuccess, loginMsg, loggedInUser) = await authService.LoginAsync("business_admin", "StoreAdmin@2026");
        loginSuccess.Should().BeTrue();
        loggedInUser.Should().NotBeNull();
        loggedInUser!.Role.Should().Be(UserRole.BusinessAdmin);

        // =========================================================================
        // STEP 5: STORE INVENTORY SETUP & POS BILLING TRANSACTION
        // =========================================================================
        var item1 = new Product
        {
            Name = "Organic Whole Wheat Flour 5KG",
            SKU = "FLOUR-WHEAT-5K",
            Barcode = "8901122334455",
            HSNCode = "1101",
            SellingPrice = 280.00m,
            GSTRate = 5.00m,
            CurrentStock = 50,
            MinStockAlert = 5
        };
        await productRepo.CreateAsync(item1);

        // POS Transaction: Customer buys 2 packets
        decimal qty = 2;
        decimal unitPrice = item1.SellingPrice;
        decimal taxable = qty * unitPrice; // 560
        decimal cgst = Math.Round(taxable * (item1.GSTRate / 200), 2); // 14
        decimal sgst = cgst; // 14
        decimal grandTotal = taxable + cgst + sgst; // 588

        string invoiceNum = await invoiceRepo.GetNextInvoiceNumberAsync(company.InvoicePrefix);
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNum,
            CustomerName = "Rajesh Sharma",
            CustomerPhone = "+91 9876500000",
            SubTotal = taxable,
            TaxableAmount = taxable,
            TotalCGST = cgst,
            TotalSGST = sgst,
            GrandTotal = grandTotal,
            AmountPaid = grandTotal,
            PaymentMethod = PaymentMethod.UPI,
            CashierUsername = loggedInUser.Username,
            Items = new List<InvoiceItem>
            {
                new InvoiceItem
                {
                    ProductId = item1.Id,
                    ProductName = item1.Name,
                    SKU = item1.SKU,
                    HSNCode = item1.HSNCode,
                    Quantity = qty,
                    UnitPrice = unitPrice,
                    TaxableValue = taxable,
                    GSTRate = item1.GSTRate,
                    CGSTAmount = cgst,
                    SGSTAmount = sgst,
                    TotalAmount = grandTotal
                }
            }
        };

        // Save invoice and deduct stock
        await invoiceRepo.SaveInvoiceAtomicAsync(invoice);
        await productRepo.AdjustStockAsync(item1.Id, -qty);

        // =========================================================================
        // STEP 6: VERIFY LEDGERS, STOCK, LICENSE & AUDIT INTEGRITY
        // =========================================================================
        var savedInvoice = await invoiceRepo.GetByInvoiceNumberAsync(invoiceNum);
        savedInvoice.Should().NotBeNull();
        savedInvoice!.GrandTotal.Should().Be(588.00m);
        savedInvoice.PaymentMethod.Should().Be(PaymentMethod.UPI);

        // Stock deduction check (50 - 2 = 48)
        var updatedProduct = await productRepo.GetByIdAsync(item1.Id);
        updatedProduct!.CurrentStock.Should().Be(48);

        // License active check
        var license = await licenseRepo.GetCurrentLicenseAsync();
        _licenseManager.EvaluateLicenseStatus(license!, DateTime.UtcNow).Should().Be(LicenseStatus.Active);
        _licenseManager.CanPerformBilling(license!, DateTime.UtcNow).Should().BeTrue();

        // Audit Trail Check
        var auditLogs = (await auditRepo.GetRecentLogsAsync(20)).ToList();
        auditLogs.Should().Contain(l => l.Action == AuditActionType.DataKeyUploaded);
        auditLogs.Should().Contain(l => l.Action == AuditActionType.Login);
    }
}
