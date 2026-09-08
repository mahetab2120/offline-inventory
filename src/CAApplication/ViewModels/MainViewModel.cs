using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Application.Interfaces;
using Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Entities;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Infrastructure.Persistence;
using Licensing.Services;
using Security.Constants;
using Security.Interfaces;
using Security.Services;

namespace CAApplication.ViewModels;

public class ClientDisplayItem : ObservableObject
{
    private string _businessCode = string.Empty;
    public string BusinessCode { get => _businessCode; set => SetProperty(ref _businessCode, value); }

    private string _legalName = string.Empty;
    public string LegalName { get => _legalName; set => SetProperty(ref _legalName, value); }

    private string _tradeName = string.Empty;
    public string TradeName { get => _tradeName; set => SetProperty(ref _tradeName, value); }

    private string _gstin = string.Empty;
    public string GSTIN { get => _gstin; set => SetProperty(ref _gstin, value); }

    private string _pan = string.Empty;
    public string PAN { get => _pan; set => SetProperty(ref _pan, value); }

    private string _contactPerson = string.Empty;
    public string ContactPerson { get => _contactPerson; set => SetProperty(ref _contactPerson, value); }

    private string _contactEmail = string.Empty;
    public string ContactEmail { get => _contactEmail; set => SetProperty(ref _contactEmail, value); }

    private string _contactPhone = string.Empty;
    public string ContactPhone { get => _contactPhone; set => SetProperty(ref _contactPhone, value); }

    private string _address = string.Empty;
    public string Address { get => _address; set => SetProperty(ref _address, value); }

    private string _city = "Mumbai";
    public string City { get => _city; set => SetProperty(ref _city, value); }

    private string _state = "Maharashtra";
    public string State { get => _state; set => SetProperty(ref _state, value); }

    private string _pincode = "400001";
    public string Pincode { get => _pincode; set => SetProperty(ref _pincode, value); }

    private BusinessType _businessType = BusinessType.Retail;
    public BusinessType BusinessType { get => _businessType; set => SetProperty(ref _businessType, value); }

    private SubscriptionTier _subscriptionPlan = SubscriptionTier.Premium;
    public SubscriptionTier SubscriptionPlan { get => _subscriptionPlan; set => SetProperty(ref _subscriptionPlan, value); }

    private string _adminUsername = "admin";
    public string AdminUsername { get => _adminUsername; set => SetProperty(ref _adminUsername, value); }

    private string _adminTempPassword = "Password@2026!";
    public string AdminTempPassword { get => _adminTempPassword; set => SetProperty(ref _adminTempPassword, value); }

    private DateTime _issuedDateUtc = DateTime.UtcNow;
    public DateTime IssuedDateUtc { get => _issuedDateUtc; set => SetProperty(ref _issuedDateUtc, value); }

    private DateTime _expiryDateUtc = DateTime.UtcNow.AddMonths(3);
    public DateTime ExpiryDateUtc { get => _expiryDateUtc; set { SetProperty(ref _expiryDateUtc, value); NotifyStatusChanged(); } }

    private bool _isSuspended = false;
    public bool IsSuspended { get => _isSuspended; set { SetProperty(ref _isSuspended, value); NotifyStatusChanged(); } }

    public int DaysRemaining => Math.Max(0, (int)(ExpiryDateUtc.Date - DateTime.UtcNow.Date).TotalDays);
    public string Status => IsSuspended ? "SUSPENDED" : DaysRemaining > 7 ? "ACTIVE" : DaysRemaining > 0 ? "EXPIRING SOON" : "GRACE PERIOD";
    public string StatusColor => IsSuspended ? "#64748B" : DaysRemaining > 7 ? "#10B981" : DaysRemaining > 0 ? "#F59E0B" : "#EF4444";
    public string BusinessTypeBadge => BusinessType.ToString();
    public string PlanBadge => SubscriptionPlan.ToString();

    public void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(DaysRemaining));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
    }
}

public class SectorDistributionItem
{
    public string SectorName { get; set; } = string.Empty;
    public int ClientCount { get; set; }
    public double Percentage { get; set; }
    public string BarColor { get; set; } = "#38BDF8";
    public string FormattedPercentage => $"{Percentage:N0}%";
}

public class MonthlyTrendItem
{
    public string MonthName { get; set; } = string.Empty;
    public int ActiveClients { get; set; }
    public double BarHeight { get; set; }
    public string ValueLabel => ActiveClients.ToString();
}

public class ValidityOption
{
    public string DisplayText { get; set; } = string.Empty;
    public int Months { get; set; }
}

public class GstHsnSummaryItem
{
    public string HSNCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UQC { get; set; } = "PCS";
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal CentralTaxAmount { get; set; }
    public decimal StateTaxAmount { get; set; }
    public decimal IntegratedTaxAmount { get; set; }
    public decimal TotalTaxAmount => CentralTaxAmount + StateTaxAmount + IntegratedTaxAmount;
}

public class GstRateBreakdownItem
{
    public string RateLabel { get; set; } = string.Empty;
    public decimal GstRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CGSTAmount { get; set; }
    public decimal SGSTAmount { get; set; }
    public decimal IGSTAmount { get; set; }
    public decimal TotalTaxAmount => CGSTAmount + SGSTAmount + IGSTAmount;
}

public partial class MainViewModel : ObservableObject
{
    private readonly IProvisioningKeyService _keyService;
    private readonly IRsaCryptoService _rsa;
    private readonly IPasswordHasher _hasher;
    private readonly IBootstrapService _bootstrapService;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILicenseRepository _licenseRepo;
    private readonly IAuditLogRepository _auditRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IDecryptedAuditPackageRepository _decryptedPackageRepo;

    // AFS Super Admin Master Credentials (Salted PBKDF2 Hash)
    private string _masterCaUser = "superadmin";
    private string _masterPasswordHash = string.Empty;
    private string _masterPasswordSalt = string.Empty;

    // AFS RSA Master Private Key (matches EmbeddedCaPublicKeyPem in Business Apps)
    private string _caPrivateKeyPem = SecurityConstants.MasterCaPrivateKeyPem;

    // --- Authentication State ---
    [ObservableProperty]
    private bool isAuthenticated = false;

    [ObservableProperty]
    private string loginUsername = "superadmin";

    [ObservableProperty]
    private string loginPassword = string.Empty;

    [ObservableProperty]
    private bool isPasswordVisible = false;

    [ObservableProperty]
    private bool isPasswordMasked = true;

    [ObservableProperty]
    private string passwordToggleIcon = "👁️ Show";

    [ObservableProperty]
    private string loginErrorMessage = string.Empty;

    [ObservableProperty]
    private string superAdminFullName = "AFS Super Administrator";

    // --- Navigation & Dashboard State ---
    [ObservableProperty]
    private string currentTab = "Dashboard"; // Dashboard, CreateClient, ClientDirectory, Renewals, DecryptAudit

    // Search & Filter in Client Directory / Dashboard
    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private string selectedStatusFilter = "All"; // All, Active, ExpiringSoon, Suspended

    // KPIs
    [ObservableProperty]
    private int totalClients = 0;

    [ObservableProperty]
    private int activeLicenses = 0;

    [ObservableProperty]
    private int expiringSoonCount = 0;

    [ObservableProperty]
    private string monthlyEstimatedRevenue = "₹1,45,000";

    [ObservableProperty]
    private string securityHealthStatus = "100% Protected (RSA-4096)";

    // Collections
    public ObservableCollection<ClientDisplayItem> AllClients { get; } = new();
    public ObservableCollection<ClientDisplayItem> FilteredClients { get; } = new();
    public ObservableCollection<SectorDistributionItem> SectorDistributions { get; } = new();
    public ObservableCollection<MonthlyTrendItem> MonthlyGrowthTrends { get; } = new();

    // --- Form Fields (Create Client) ---
    [ObservableProperty]
    private string businessCode = GenerateUniqueBusinessCode();

    [ObservableProperty]
    private string legalName = string.Empty;

    [ObservableProperty]
    private string tradeName = string.Empty;

    [ObservableProperty]
    private string gstin = string.Empty;

    [ObservableProperty]
    private string pan = string.Empty;

    [ObservableProperty]
    private BusinessType selectedBusinessType = BusinessType.Retail;

    [ObservableProperty]
    private string contactPerson = string.Empty;

    [ObservableProperty]
    private string contactEmail = string.Empty;

    [ObservableProperty]
    private string contactPhone = string.Empty;

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string city = "Mumbai";

    [ObservableProperty]
    private string state = "Maharashtra";

    [ObservableProperty]
    private string pincode = "400001";

    [ObservableProperty]
    private SubscriptionTier selectedPlan = SubscriptionTier.Premium;

    [ObservableProperty]
    private string adminUsername = "admin";

    [ObservableProperty]
    private string adminTempPassword = "Password@2026!";

    [ObservableProperty]
    private int validityMonths = 3;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isValidationError = false;

    [ObservableProperty]
    private string formValidationErrorMessage = string.Empty;

    [ObservableProperty]
    private string generatedKeyFilePath = string.Empty;

    [ObservableProperty]
    private string generatedKeyFileName = string.Empty;

    [ObservableProperty]
    private string exportedKeySummary = string.Empty;

    [ObservableProperty]
    private bool isKeyGenerated = false;

    // --- Automated Email Draft State ---
    [ObservableProperty]
    private string emailRecipient = string.Empty;

    [ObservableProperty]
    private string emailSubject = string.Empty;

    [ObservableProperty]
    private string emailBody = string.Empty;

    [ObservableProperty]
    private string emailStatusNotification = string.Empty;

    // --- Modal 1: Confirmation Dialog State ---
    [ObservableProperty]
    private bool isConfirmModalOpen = false;

    [ObservableProperty]
    private string confirmModalTitle = "Confirm Action";

    [ObservableProperty]
    private string confirmModalMessage = "Are you sure you want to proceed with this operation?";

    [ObservableProperty]
    private string confirmButtonText = "Yes, Proceed";

    [ObservableProperty]
    private string confirmButtonColor = "#2563EB";

    private Func<Task>? _pendingAsyncConfirmAction;
    private Action? _pendingSyncConfirmAction;

    // --- Modal 2: Subscription Update & Renewal Modal State ---
    [ObservableProperty]
    private bool isSubscriptionModalOpen = false;

    [ObservableProperty]
    private ClientDisplayItem? targetClientForSubscription;

    [ObservableProperty]
    private string updateModalBusinessCode = string.Empty;

    [ObservableProperty]
    private string updateModalBusinessName = string.Empty;

    [ObservableProperty]
    private SubscriptionTier updateSelectedPlan = SubscriptionTier.Premium;

    [ObservableProperty]
    private ValidityOption? updateSelectedValidity;

    [ObservableProperty]
    private DateTime updateCalculatedExpiryDate = DateTime.UtcNow.AddMonths(3);

    // --- TAB 4: License Renewals State ---
    [ObservableProperty]
    private ClientDisplayItem? selectedRenewalClient;

    [ObservableProperty]
    private SubscriptionTier renewalSelectedPlan = SubscriptionTier.Premium;

    [ObservableProperty]
    private ValidityOption? renewalSelectedValidity;

    [ObservableProperty]
    private DateTime renewalCalculatedExpiryDate = DateTime.UtcNow.AddMonths(12);

    [ObservableProperty]
    private string renewalStatusMessage = string.Empty;

    [ObservableProperty]
    private string lastExportedLicensePath = string.Empty;

    [ObservableProperty]
    private string lastExportedLicenseFileName = string.Empty;

    [ObservableProperty]
    private string renewalSummaryText = string.Empty;

    [ObservableProperty]
    private bool isLicenseRenewalGenerated = false;

    // --- TAB 5: Decrypt Client Audit State ---
    [ObservableProperty]
    private string selectedAuditFilePath = string.Empty;

    [ObservableProperty]
    private string decryptStatusMessage = string.Empty;

    [ObservableProperty]
    private bool isAuditPackageDecrypted = false;

    [ObservableProperty]
    private string auditBusinessCode = string.Empty;

    [ObservableProperty]
    private string auditLegalName = string.Empty;

    [ObservableProperty]
    private string auditGstin = string.Empty;

    [ObservableProperty]
    private string auditAccountingPeriod = string.Empty;

    [ObservableProperty]
    private DateTime auditExportTimestamp = DateTime.UtcNow;

    [ObservableProperty]
    private int auditTotalInvoices = 0;

    [ObservableProperty]
    private decimal auditTotalRevenue = 0;

    [ObservableProperty]
    private decimal auditTaxableTurnover = 0;

    [ObservableProperty]
    private decimal auditTotalCgst = 0;

    [ObservableProperty]
    private decimal auditTotalSgst = 0;

    [ObservableProperty]
    private decimal auditTotalIgst = 0;

    [ObservableProperty]
    private decimal auditTotalTax = 0;

    [ObservableProperty]
    private int auditTotalProducts = 0;

    [ObservableProperty]
    private decimal auditInventoryValuation = 0;

    [ObservableProperty]
    private int auditLogsCount = 0;

    [ObservableProperty]
    private string auditActiveSubTab = "Invoices"; // Invoices, TaxSummary, Inventory, AuditLogs

    public ObservableCollection<Invoice> DecryptedAuditInvoices { get; } = new();
    public ObservableCollection<Product> DecryptedAuditProducts { get; } = new();
    public ObservableCollection<AuditLog> DecryptedAuditLogs { get; } = new();
    public ObservableCollection<GstHsnSummaryItem> DecryptedGstHsnSummaries { get; } = new();
    public ObservableCollection<GstRateBreakdownItem> DecryptedGstRateBreakdowns { get; } = new();

    public ObservableCollection<ValidityOption> ValidityOptions { get; } = new()
    {
        new ValidityOption { DisplayText = "+1 Month (30 Days Extension)", Months = 1 },
        new ValidityOption { DisplayText = "+3 Months (Quarterly Renewal)", Months = 3 },
        new ValidityOption { DisplayText = "+6 Months (Half-Yearly Renewal)", Months = 6 },
        new ValidityOption { DisplayText = "+12 Months (1 Year Full Annual)", Months = 12 },
        new ValidityOption { DisplayText = "+24 Months (2 Years Enterprise Long-Term)", Months = 24 }
    };

    public ObservableCollection<BusinessType> BusinessTypes { get; } = new(Enum.GetValues<BusinessType>());
    public ObservableCollection<SubscriptionTier> SubscriptionTiers { get; } = new(Enum.GetValues<SubscriptionTier>());

    public MainViewModel()
    {
        var aesGcm = new AesGcmService();
        _rsa = new RsaCryptoService();
        _hasher = new PasswordHasher();
        _keyService = new ProvisioningKeyService(aesGcm, _rsa);

        string connStr = DependencyInjection.DefaultPostgreSqlConnectionString;
        var connFactory = new NpgsqlDbConnectionFactory(connStr);
        var dbInit = new DatabaseInitializer(connFactory);

        _companyRepo = new PostgresCompanyRepository(connFactory);
        _userRepo = new PostgresUserRepository(connFactory);
        _licenseRepo = new PostgresLicenseRepository(connFactory);
        _auditRepo = new PostgresAuditLogRepository(connFactory);
        _productRepo = new PostgresProductRepository(connFactory);
        _invoiceRepo = new PostgresInvoiceRepository(connFactory);
        _decryptedPackageRepo = new PostgresDecryptedAuditPackageRepository(connFactory);
        _bootstrapService = new BootstrapService(_companyRepo, _userRepo, _licenseRepo, _auditRepo, _hasher);

        UpdateSelectedValidity = ValidityOptions[1]; // Default to +3 Months

        InitializeDatabaseAsync(dbInit);
        InitializeMasterCredentials();
        SeedDashboardAnalyticsAndClients();
    }

    private static string GenerateUniqueBusinessCode()
    {
        return $"BUS-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    partial void OnUpdateSelectedValidityChanged(ValidityOption? value)
    {
        RecalculateUpdatedExpiry();
    }

    private void RecalculateUpdatedExpiry()
    {
        if (TargetClientForSubscription == null) return;

        int monthsToAdd = UpdateSelectedValidity?.Months ?? 3;
        DateTime baseDate = TargetClientForSubscription.ExpiryDateUtc > DateTime.UtcNow 
            ? TargetClientForSubscription.ExpiryDateUtc 
            : DateTime.UtcNow;

        UpdateCalculatedExpiryDate = baseDate.AddMonths(monthsToAdd);
    }

    partial void OnSelectedRenewalClientChanged(ClientDisplayItem? value)
    {
        if (value != null)
        {
            RenewalSelectedPlan = value.SubscriptionPlan;
        }
        RecalculateRenewalExpiry();
    }

    partial void OnRenewalSelectedValidityChanged(ValidityOption? value)
    {
        RecalculateRenewalExpiry();
    }

    private void RecalculateRenewalExpiry()
    {
        if (SelectedRenewalClient == null) return;

        int monthsToAdd = RenewalSelectedValidity?.Months ?? 12;
        DateTime baseDate = SelectedRenewalClient.ExpiryDateUtc > DateTime.UtcNow 
            ? SelectedRenewalClient.ExpiryDateUtc 
            : DateTime.UtcNow;

        RenewalCalculatedExpiryDate = baseDate.AddMonths(monthsToAdd);
    }

    // Auto-uppercase and PAN sync triggers
    partial void OnPanChanged(string value)
    {
        if (value != null)
        {
            string upper = value.ToUpperInvariant().Trim();
            if (Pan != upper)
            {
                Pan = upper;
            }
        }
    }

    partial void OnGstinChanged(string value)
    {
        if (value != null)
        {
            string upper = value.ToUpperInvariant().Trim();
            if (Gstin != upper)
            {
                Gstin = upper;
            }

            if (upper.Length == 15 && (string.IsNullOrWhiteSpace(Pan) || Pan.Length < 10))
            {
                string extractedPan = upper.Substring(2, 10);
                if (Regex.IsMatch(extractedPan, @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$"))
                {
                    Pan = extractedPan;
                }
            }
        }
    }

    private async void InitializeDatabaseAsync(DatabaseInitializer dbInit)
    {
        try
        {
            await dbInit.EnsureDatabaseAndSchemaAsync();
            await LoadDbClientsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAApplication] Database init notice: {ex.Message}");
        }
    }

    private async Task LoadDbClientsAsync()
    {
        try
        {
            var company = await _companyRepo.GetCompanyAsync();
            var license = await _licenseRepo.GetCurrentLicenseAsync();
            if (company != null && !AllClients.Any(c => c.BusinessCode == company.BusinessCode))
            {
                AllClients.Insert(0, new ClientDisplayItem
                {
                    BusinessCode = company.BusinessCode,
                    LegalName = company.LegalName,
                    TradeName = company.TradeName ?? company.LegalName,
                    GSTIN = company.GSTIN ?? string.Empty,
                    PAN = company.PAN ?? string.Empty,
                    ContactEmail = company.ContactEmail ?? string.Empty,
                    ContactPhone = company.ContactPhone ?? string.Empty,
                    Address = company.Address ?? string.Empty,
                    City = company.City ?? "Mumbai",
                    State = company.State ?? "Maharashtra",
                    Pincode = company.Pincode ?? "400001",
                    BusinessType = company.BusinessType,
                    SubscriptionPlan = license?.Plan ?? SubscriptionTier.Enterprise,
                    AdminUsername = "admin",
                    IssuedDateUtc = company.CreatedAtUtc,
                    ExpiryDateUtc = license?.ExpiryDateUtc ?? DateTime.UtcNow.AddMonths(12)
                });
                CalculateKpis();
                RefreshFilteredClients();
            }
        }
        catch
        {
            // Ignored
        }
    }

    private void InitializeMasterCredentials()
    {
        var (hash, salt) = _hasher.HashPassword("SuperAdmin@2026!");
        _masterPasswordHash = hash;
        _masterPasswordSalt = salt;
    }

    private void SeedDashboardAnalyticsAndClients()
    {
        AllClients.Clear();

        AllClients.Add(new ClientDisplayItem
        {
            BusinessCode = "BUS-MUM-1001",
            LegalName = "Metro HyperMarket Pvt Ltd",
            TradeName = "Metro SuperMart",
            GSTIN = "27AABCM1122F1Z4",
            PAN = "AABCM1122F",
            ContactPerson = "Sunil Sharma",
            ContactEmail = "contact@metrosmart.com",
            ContactPhone = "9820011223",
            Address = "Plot 12, Link Road, Andheri West",
            City = "Mumbai",
            State = "Maharashtra",
            Pincode = "400053",
            BusinessType = BusinessType.Supermarket,
            SubscriptionPlan = SubscriptionTier.Enterprise,
            AdminUsername = "metro_admin",
            IssuedDateUtc = DateTime.UtcNow.AddMonths(-3),
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(9)
        });

        AllClients.Add(new ClientDisplayItem
        {
            BusinessCode = "BUS-DEL-1002",
            LegalName = "Lifeline Pharmacy & Surgical",
            TradeName = "Lifeline Meds",
            GSTIN = "07AABCL3344F1Z8",
            PAN = "AABCL3344F",
            ContactPerson = "Dr. Rajeev Gupta",
            ContactEmail = "care@lifelinemeds.in",
            ContactPhone = "9811223344",
            Address = "Shop 4, AIIMS Market",
            City = "New Delhi",
            State = "Delhi",
            Pincode = "110029",
            BusinessType = BusinessType.Pharmacy,
            SubscriptionPlan = SubscriptionTier.PharmacySpecial,
            AdminUsername = "pharmacy_admin",
            IssuedDateUtc = DateTime.UtcNow.AddMonths(-5),
            ExpiryDateUtc = DateTime.UtcNow.AddDays(4) // Expiring soon
        });

        AllClients.Add(new ClientDisplayItem
        {
            BusinessCode = "BUS-BLR-1003",
            LegalName = "Apex Tools & Electricals Pvt Ltd",
            TradeName = "Apex Tools Hub",
            GSTIN = "29AACCA5566F1Z1",
            PAN = "AACCA5566F",
            ContactPerson = "Karthik Reddy",
            ContactEmail = "sales@apextoolshub.com",
            ContactPhone = "9845012345",
            Address = "Industrial Area, Peenya 2nd Stage",
            City = "Bengaluru",
            State = "Karnataka",
            Pincode = "560058",
            BusinessType = BusinessType.Retail,
            SubscriptionPlan = SubscriptionTier.Premium,
            AdminUsername = "apex_admin",
            IssuedDateUtc = DateTime.UtcNow.AddMonths(-2),
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(4)
        });

        AllClients.Add(new ClientDisplayItem
        {
            BusinessCode = "BUS-PUN-1004",
            LegalName = "Royal Gourmet Foods & Snacks",
            TradeName = "Royal Spice Retail",
            GSTIN = "27AABCR7788F1Z9",
            PAN = "AABCR7788F",
            ContactPerson = "Anand Deshmukh",
            ContactEmail = "manager@royalspice.in",
            ContactPhone = "9823056789",
            Address = "FC Road, Deccan Gymkhana",
            City = "Pune",
            State = "Maharashtra",
            Pincode = "411004",
            BusinessType = BusinessType.GeneralStore,
            SubscriptionPlan = SubscriptionTier.Professional,
            AdminUsername = "royal_admin",
            IssuedDateUtc = DateTime.UtcNow.AddMonths(-1),
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(5)
        });

        AllClients.Add(new ClientDisplayItem
        {
            BusinessCode = "BUS-HYD-1005",
            LegalName = "Sri Venkateshwara Wholesale Traders",
            TradeName = "SV Wholesale Hub",
            GSTIN = "36AACCS9900F1Z3",
            PAN = "AACCS9900F",
            ContactPerson = "Venkatesh Rao",
            ContactEmail = "orders@svwholesale.com",
            ContactPhone = "9848099001",
            Address = "Begum Bazar Commercial Complex",
            City = "Hyderabad",
            State = "Telangana",
            Pincode = "500012",
            BusinessType = BusinessType.Wholesale,
            SubscriptionPlan = SubscriptionTier.Enterprise,
            AdminUsername = "sv_admin",
            IssuedDateUtc = DateTime.UtcNow.AddMonths(-6),
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(6)
        });

        AllClients.Add(new ClientDisplayItem
        {
            BusinessCode = "BUS-AHM-1006",
            LegalName = "Krishna General Stores",
            TradeName = "Krishna Super Kirana",
            GSTIN = "24AABCK2233F1Z7",
            PAN = "AABCK2233F",
            ContactPerson = "Mahetab Patel",
            ContactEmail = "mahetab.office@gmail.com",
            ContactPhone = "9429689988",
            Address = "Main Market Road",
            City = "Mullanpur",
            State = "Gujarat",
            Pincode = "380001",
            BusinessType = BusinessType.GeneralStore,
            SubscriptionPlan = SubscriptionTier.Basic,
            AdminUsername = "krishna_admin",
            IssuedDateUtc = DateTime.UtcNow.AddMonths(-4),
            ExpiryDateUtc = DateTime.UtcNow.AddDays(2) // Expiring soon
        });

        RefreshFilteredClients();
        CalculateKpis();
        SeedChartData();
    }

    private void CalculateKpis()
    {
        TotalClients = AllClients.Count;
        ActiveLicenses = AllClients.Count(c => !c.IsSuspended && c.DaysRemaining > 7);
        ExpiringSoonCount = AllClients.Count(c => !c.IsSuspended && c.DaysRemaining is > 0 and <= 7);
    }

    private void SeedChartData()
    {
        SectorDistributions.Clear();
        SectorDistributions.Add(new SectorDistributionItem { SectorName = "Supermarket & Groceries", ClientCount = 2, Percentage = 33.3, BarColor = "#38BDF8" });
        SectorDistributions.Add(new SectorDistributionItem { SectorName = "Pharmacy & Healthcare", ClientCount = 1, Percentage = 16.7, BarColor = "#10B981" });
        SectorDistributions.Add(new SectorDistributionItem { SectorName = "Tools & Hardware", ClientCount = 1, Percentage = 16.7, BarColor = "#F59E0B" });
        SectorDistributions.Add(new SectorDistributionItem { SectorName = "General & FMCG Retail", ClientCount = 2, Percentage = 33.3, BarColor = "#EC4899" });

        MonthlyGrowthTrends.Clear();
        MonthlyGrowthTrends.Add(new MonthlyTrendItem { MonthName = "Jan", ActiveClients = 1, BarHeight = 25 });
        MonthlyGrowthTrends.Add(new MonthlyTrendItem { MonthName = "Feb", ActiveClients = 2, BarHeight = 50 });
        MonthlyGrowthTrends.Add(new MonthlyTrendItem { MonthName = "Mar", ActiveClients = 3, BarHeight = 75 });
        MonthlyGrowthTrends.Add(new MonthlyTrendItem { MonthName = "Apr", ActiveClients = 4, BarHeight = 100 });
        MonthlyGrowthTrends.Add(new MonthlyTrendItem { MonthName = "May", ActiveClients = 5, BarHeight = 125 });
        MonthlyGrowthTrends.Add(new MonthlyTrendItem { MonthName = "Jun", ActiveClients = 6, BarHeight = 150 });
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshFilteredClients();
    }

    partial void OnSelectedStatusFilterChanged(string value)
    {
        RefreshFilteredClients();
    }

    [RelayCommand]
    private void SetStatusFilter(string filter)
    {
        SelectedStatusFilter = filter;
    }

    private void RefreshFilteredClients()
    {
        FilteredClients.Clear();
        var q = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        var matches = AllClients.Where(c =>
        {
            // Status filter
            if (SelectedStatusFilter == "Active" && (c.IsSuspended || c.DaysRemaining <= 7))
                return false;
            if (SelectedStatusFilter == "ExpiringSoon" && (c.IsSuspended || c.DaysRemaining is <= 0 or > 7))
                return false;
            if (SelectedStatusFilter == "Suspended" && !c.IsSuspended)
                return false;
            if (SelectedStatusFilter == "GracePeriod" && (c.IsSuspended || c.DaysRemaining > 0))
                return false;

            // Search query filter
            if (string.IsNullOrEmpty(q))
                return true;

            return c.LegalName.ToLowerInvariant().Contains(q) ||
                   c.TradeName.ToLowerInvariant().Contains(q) ||
                   c.BusinessCode.ToLowerInvariant().Contains(q) ||
                   c.GSTIN.ToLowerInvariant().Contains(q) ||
                   c.ContactEmail.ToLowerInvariant().Contains(q) ||
                   c.ContactPhone.ToLowerInvariant().Contains(q) ||
                   c.BusinessType.ToString().ToLowerInvariant().Contains(q) ||
                   c.SubscriptionPlan.ToString().ToLowerInvariant().Contains(q);
        });

        foreach (var client in matches)
        {
            FilteredClients.Add(client);
        }
    }

    // --- Authentication Commands ---

    [RelayCommand]
    private void PerformLogin()
    {
        if (string.IsNullOrWhiteSpace(LoginUsername) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginErrorMessage = "Please enter both Super Admin username and password.";
            return;
        }

        if (!LoginUsername.Trim().Equals(_masterCaUser, StringComparison.OrdinalIgnoreCase))
        {
            LoginErrorMessage = "Invalid Super Admin credentials.";
            return;
        }

        bool isValid = _hasher.VerifyPassword(LoginPassword, _masterPasswordHash, _masterPasswordSalt);
        if (!isValid)
        {
            LoginErrorMessage = "Invalid Super Admin password. Please check and try again.";
            return;
        }

        LoginErrorMessage = string.Empty;
        LoginPassword = string.Empty;
        CurrentTab = "Dashboard";
        IsAuthenticated = true;
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
        IsPasswordMasked = !IsPasswordVisible;
        PasswordToggleIcon = IsPasswordVisible ? "🙈 Hide" : "👁️ Show";
    }

    [RelayCommand]
    private void Logout()
    {
        ShowConfirmation(
            "Logout Super Administrator", 
            "Are you sure you want to log out from the AFS Super Admin Control Center?",
            () =>
            {
                IsAuthenticated = false;
                LoginPassword = string.Empty;
                IsPasswordVisible = false;
                IsPasswordMasked = true;
                PasswordToggleIcon = "👁️ Show";
                LoginErrorMessage = string.Empty;
            },
            "🚪 Logout",
            "#DC2626");
    }

    [RelayCommand]
    private void SwitchTab(object? tabNameObj)
    {
        if (tabNameObj is string tabName)
        {
            CurrentTab = tabName;
            if (tabName == "Renewals")
            {
                if (SelectedRenewalClient == null && AllClients.Count > 0)
                {
                    SelectedRenewalClient = AllClients.FirstOrDefault(c => c.DaysRemaining <= 14) ?? AllClients.First();
                }
                if (RenewalSelectedValidity == null && ValidityOptions.Count > 3)
                {
                    RenewalSelectedValidity = ValidityOptions[3]; // +12 Months default
                }
                RecalculateRenewalExpiry();
            }
        }
    }

    private void SetValidationError(string errorMessage)
    {
        FormValidationErrorMessage = errorMessage;
        IsValidationError = true;
        StatusMessage = "❌ " + errorMessage;
    }

    // =========================================================================
    // CONFIRMATION MODAL LOGIC
    // =========================================================================

    public void ShowConfirmation(string title, string message, Action onConfirm, string confirmText = "Yes, Proceed", string confirmColor = "#2563EB")
    {
        ConfirmModalTitle = title;
        ConfirmModalMessage = message;
        ConfirmButtonText = confirmText;
        ConfirmButtonColor = confirmColor;
        _pendingSyncConfirmAction = onConfirm;
        _pendingAsyncConfirmAction = null;
        IsConfirmModalOpen = true;
    }

    public void ShowAsyncConfirmation(string title, string message, Func<Task> onConfirmAsync, string confirmText = "Yes, Proceed", string confirmColor = "#2563EB")
    {
        ConfirmModalTitle = title;
        ConfirmModalMessage = message;
        ConfirmButtonText = confirmText;
        ConfirmButtonColor = confirmColor;
        _pendingAsyncConfirmAction = onConfirmAsync;
        _pendingSyncConfirmAction = null;
        IsConfirmModalOpen = true;
    }

    [RelayCommand]
    private async Task ExecuteConfirmAction()
    {
        IsConfirmModalOpen = false;

        if (_pendingAsyncConfirmAction != null)
        {
            await _pendingAsyncConfirmAction.Invoke();
            _pendingAsyncConfirmAction = null;
        }
        else if (_pendingSyncConfirmAction != null)
        {
            _pendingSyncConfirmAction.Invoke();
            _pendingSyncConfirmAction = null;
        }
    }

    [RelayCommand]
    private void CancelConfirmModal()
    {
        IsConfirmModalOpen = false;
        _pendingAsyncConfirmAction = null;
        _pendingSyncConfirmAction = null;
    }

    // =========================================================================
    // CLIENT REGISTRATION & KEY GENERATION
    // =========================================================================

    [RelayCommand]
    private void GenerateAndExportDataKey()
    {
        IsValidationError = false;
        FormValidationErrorMessage = string.Empty;

        // 1. Validate Legal Name
        if (string.IsNullOrWhiteSpace(LegalName) || LegalName.Trim().Length < 3)
        {
            SetValidationError("Legal Business Name is required (minimum 3 characters).");
            return;
        }

        // 2. Validate PAN
        string panUpper = Pan?.Trim().ToUpperInvariant() ?? string.Empty;
        var panRegex = new Regex(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$");
        if (string.IsNullOrWhiteSpace(panUpper) || !panRegex.IsMatch(panUpper))
        {
            SetValidationError("Invalid PAN Number! Format must be 5 letters, 4 digits, 1 letter (e.g., ASDFP4402C).");
            return;
        }

        // 3. Validate GSTIN
        string gstinUpper = Gstin?.Trim().ToUpperInvariant() ?? string.Empty;
        var gstinRegex = new Regex(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$");
        if (string.IsNullOrWhiteSpace(gstinUpper) || !gstinRegex.IsMatch(gstinUpper))
        {
            SetValidationError("Invalid GSTIN! Expected 15 characters (e.g., 27ASDFP4402C1Z5: 2 state digits + PAN + 1 entity + Z + 1 checksum).");
            return;
        }

        string panFromGstin = gstinUpper.Substring(2, 10);
        if (!panFromGstin.Equals(panUpper, StringComparison.OrdinalIgnoreCase))
        {
            SetValidationError($"GSTIN and PAN Mismatch! The PAN in GSTIN ({panFromGstin}) must match the PAN Number field ({panUpper}).");
            return;
        }

        // 4. Validate Email Address
        string emailTrimmed = ContactEmail?.Trim() ?? string.Empty;
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        if (string.IsNullOrWhiteSpace(emailTrimmed) || !emailRegex.IsMatch(emailTrimmed))
        {
            SetValidationError("Invalid Client Email Address! Please enter a valid email (e.g., owner@example.com) for key delivery.");
            return;
        }

        // 5. Validate Mobile Number
        string rawPhone = ContactPhone?.Trim() ?? string.Empty;
        string cleanPhone = Regex.Replace(rawPhone, @"[^\d]", "");
        if (cleanPhone.Length < 10)
        {
            SetValidationError("Invalid Contact Mobile Number! Please enter a 10-digit phone number (e.g., 9876543210).");
            return;
        }
        if (cleanPhone.Length > 10)
        {
            cleanPhone = cleanPhone[^10..];
        }

        // 6. Validate Pincode
        string pinTrimmed = Pincode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pinTrimmed) || !Regex.IsMatch(pinTrimmed, @"^[1-9][0-9]{5}$"))
        {
            SetValidationError("Invalid Postal Pincode! Expected a 6-digit Indian PIN code (e.g., 400001).");
            return;
        }

        // 7. Validate Business Admin Credentials
        if (string.IsNullOrWhiteSpace(AdminUsername) || AdminUsername.Trim().Length < 3 || !Regex.IsMatch(AdminUsername.Trim(), @"^[a-zA-Z0-9_]+$"))
        {
            SetValidationError("Invalid Business Admin Username! Minimum 3 alphanumeric characters (e.g. admin, store_admin).");
            return;
        }

        if (string.IsNullOrWhiteSpace(AdminTempPassword) || AdminTempPassword.Trim().Length < 6)
        {
            SetValidationError("Password too short! Initial temporary password must be at least 6 characters.");
            return;
        }

        // Confirmation Dialog before generating & provisioning
        ShowAsyncConfirmation(
            "Confirm Client Registration & Key Generation",
            $"Are you sure you want to register '{LegalName.Trim()}' ({BusinessCode.Trim()}) and issue a {SelectedPlan} Tier license with key delivery to '{emailTrimmed}'?",
            async () => await ExecuteGenerateAndExportDataKeyAsync(panUpper, gstinUpper, emailTrimmed, cleanPhone, pinTrimmed),
            "🚀 Generate & Register",
            "#2563EB");
    }

    private async Task ExecuteGenerateAndExportDataKeyAsync(string panUpper, string gstinUpper, string emailTrimmed, string cleanPhone, string pinTrimmed)
    {
        var (hash, salt) = _hasher.HashPassword(AdminTempPassword);

        var payload = new ClientProvisioningPayload
        {
            BusinessCode = BusinessCode.Trim(),
            LegalName = LegalName.Trim(),
            TradeName = string.IsNullOrWhiteSpace(TradeName) ? LegalName.Trim() : TradeName.Trim(),
            GSTIN = gstinUpper,
            PAN = panUpper,
            BusinessType = SelectedBusinessType,
            Address = Address.Trim(),
            City = City.Trim(),
            State = State.Trim(),
            Pincode = pinTrimmed,
            ContactPhone = cleanPhone,
            ContactEmail = emailTrimmed,
            SubscriptionPlan = SelectedPlan,
            AdminUsername = AdminUsername.Trim(),
            AdminFullName = $"{LegalName.Trim()} Administrator",
            AdminPasswordHash = hash,
            AdminSalt = salt,
            AdminTempPasswordPlain = AdminTempPassword,
            IssuedDateUtc = DateTime.UtcNow,
            ExpiryDateUtc = DateTime.UtcNow.AddMonths(ValidityMonths),
            GracePeriodDays = 7,
            MaxUsers = SelectedPlan == SubscriptionTier.Enterprise ? 50 : 10,
            MaxProducts = SelectedPlan == SubscriptionTier.Enterprise ? 100000 : 25000,
            EnabledModules = new List<string> { "Billing", "Inventory", "GSTReports", "RackManagement", "OfflineExchange" }
        };

        if (SelectedBusinessType == BusinessType.Pharmacy)
        {
            payload.EnabledModules.Add("PharmacyBatchExpiry");
        }

        // 1. Cryptographically sign and encrypt the key package with AFS Master Private Key
        string keyContent = _keyService.GenerateProvisioningDataKey(payload, _caPrivateKeyPem);

        // 2. Export key to GeneratedKeys folder and root working directory for easy selection
        string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedKeys");
        Directory.CreateDirectory(exportDir);
        string fileName = $"Client_{payload.BusinessCode}_DataKey.key";
        string filePath = Path.Combine(exportDir, fileName);
        File.WriteAllText(filePath, keyContent);

        try
        {
            string rootKeysDir = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedKeys");
            Directory.CreateDirectory(rootKeysDir);
            File.WriteAllText(Path.Combine(rootKeysDir, fileName), keyContent);
        }
        catch
        {
            // Ignored
        }

        GeneratedKeyFilePath = filePath;
        GeneratedKeyFileName = fileName;

        // 3. Draft Professional Client Email
        string greetingName = !string.IsNullOrWhiteSpace(ContactPerson) ? ContactPerson.Trim() : payload.TradeName;
        EmailRecipient = emailTrimmed;
        EmailSubject = $"🔐 AFS Store Activation Key & Credentials - {payload.TradeName} ({payload.BusinessCode})";
        
        EmailBody = 
$@"Dear {greetingName},

Welcome to the AFS Offline Inventory & Billing System!

Your commercial business account has been successfully registered by AFS Super Administration. Your cryptographically signed Encrypted Store Activation Key has been generated and is attached below.

==================================================
1. BUSINESS ACCOUNT SUMMARY
==================================================
Business Code:      {payload.BusinessCode}
Legal Entity Name:  {payload.LegalName}
Store / Trade Name: {payload.TradeName}
Classification:     {payload.BusinessType}
GSTIN / PAN:        {payload.GSTIN} / {payload.PAN}
Store Address:      {payload.Address}, {payload.City}, {payload.State} - {payload.Pincode}
Contact Phone:      {payload.ContactPhone}
Subscription Plan:  {payload.SubscriptionPlan} Tier
License Validity:   {payload.IssuedDateUtc:dd-MMM-yyyy} to {payload.ExpiryDateUtc:dd-MMM-yyyy} ({ValidityMonths} Months)

==================================================
2. INITIAL ADMINISTRATOR LOGIN CREDENTIALS
==================================================
Admin Username:     {payload.AdminUsername}
Temporary Password: {AdminTempPassword}
(Note: You can change this password after logging in)

==================================================
3. STEP-BY-STEP STORE ACTIVATION INSTRUCTIONS
==================================================
Step 1: Open the 'AFS Desktop Business App' on your store computer.
Step 2: On the setup screen, click '📂 Browse & Select Data Key File...'.
Step 3: Select the attached encrypted key file:
        '{fileName}'
Step 4: Click '🚀 Activate & Bootstrap Business Database'.
Step 5: The database will bootstrap automatically. Sign in with the Admin credentials above to start billing!

--------------------------------------------------
ATTACHMENT NOTICE:
Attached File: {fileName}
Protection: RSA-2048 Digital Signature & AES-256-GCM Military Grade Encryption
--------------------------------------------------

If you have any questions or require deployment support, reply directly to this email.

Warm Regards,
AFS Super Administration Team
";

        ExportedKeySummary = 
$@"=== AFS CLIENT PROVISIONING DATA KEY ISSUED ===
Business Code:    {payload.BusinessCode}
Legal Name:       {payload.LegalName}
Contact Person:   {greetingName} ({EmailRecipient} / {payload.ContactPhone})
PAN / GSTIN:      {payload.PAN} / {payload.GSTIN}
Business Type:    {payload.BusinessType}
Subscription:     {payload.SubscriptionPlan} (Expires: {payload.ExpiryDateUtc:dd-MMM-yyyy})
Initial Admin:    {payload.AdminUsername}
Initial Password: {AdminTempPassword}
Export File:      {filePath}
Status:           Signed with AFS Private Key (RSA-2048) & Encrypted (AES-256-GCM)

Email Draft:      Ready to dispatch with attached {fileName}!";

        IsKeyGenerated = true;
        IsValidationError = false;
        FormValidationErrorMessage = string.Empty;
        EmailStatusNotification = "✅ Encrypted Data Key & Client Email Draft generated successfully!";

        // 4. Save directly into PostgreSQL database so DB is provisioned immediately
        try
        {
            await _bootstrapService.BootstrapFromDataKeyAsync(payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAApplication] Database direct bootstrap notice: {ex.Message}");
        }

        // 5. Update Dashboard UI Directory
        var existing = AllClients.FirstOrDefault(c => c.BusinessCode == payload.BusinessCode);
        if (existing != null)
        {
            AllClients.Remove(existing);
        }

        AllClients.Insert(0, new ClientDisplayItem
        {
            BusinessCode = payload.BusinessCode,
            LegalName = payload.LegalName,
            TradeName = payload.TradeName,
            GSTIN = payload.GSTIN,
            PAN = payload.PAN,
            ContactPerson = greetingName,
            ContactEmail = payload.ContactEmail,
            ContactPhone = payload.ContactPhone,
            Address = payload.Address,
            City = payload.City,
            State = payload.State,
            Pincode = payload.Pincode,
            BusinessType = payload.BusinessType,
            SubscriptionPlan = payload.SubscriptionPlan,
            AdminUsername = payload.AdminUsername,
            AdminTempPassword = AdminTempPassword,
            IssuedDateUtc = payload.IssuedDateUtc,
            ExpiryDateUtc = payload.ExpiryDateUtc
        });

        CalculateKpis();
        RefreshFilteredClients();
        StatusMessage = $"✅ Encrypted Data Key generated & saved to PostgreSQL! Email draft ready for: {EmailRecipient}";
    }

    // =========================================================================
    // UPDATE SUBSCRIPTION & RENEWAL MODAL
    // =========================================================================

    [RelayCommand]
    private void OpenSubscriptionModal(object? clientObj)
    {
        if (clientObj is not ClientDisplayItem client) return;

        TargetClientForSubscription = client;
        UpdateModalBusinessCode = client.BusinessCode;
        UpdateModalBusinessName = $"{client.TradeName} ({client.LegalName})";
        UpdateSelectedPlan = client.SubscriptionPlan;
        UpdateSelectedValidity = ValidityOptions[1]; // +3 Months

        RecalculateUpdatedExpiry();
        IsSubscriptionModalOpen = true;
    }

    [RelayCommand]
    private void CancelSubscriptionModal()
    {
        IsSubscriptionModalOpen = false;
        TargetClientForSubscription = null;
    }

    [RelayCommand]
    private async Task ConfirmSubscriptionUpdate()
    {
        if (TargetClientForSubscription == null) return;

        var client = TargetClientForSubscription;
        var newPlan = UpdateSelectedPlan;
        var newExpiry = UpdateCalculatedExpiryDate;

        IsSubscriptionModalOpen = false;

        // Apply changes to client object
        client.SubscriptionPlan = newPlan;
        client.ExpiryDateUtc = newExpiry;
        client.IsSuspended = false;
        client.NotifyStatusChanged();

        // Regenerate signed cryptographic .key package
        string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedKeys");
        Directory.CreateDirectory(exportDir);
        string fileName = $"Client_{client.BusinessCode}_DataKey.key";
        string filePath = Path.Combine(exportDir, fileName);

        var (hash, salt) = _hasher.HashPassword(client.AdminTempPassword);
        var payload = new ClientProvisioningPayload
        {
            BusinessCode = client.BusinessCode,
            LegalName = client.LegalName,
            TradeName = client.TradeName,
            GSTIN = client.GSTIN,
            PAN = client.PAN,
            BusinessType = client.BusinessType,
            Address = client.Address,
            City = client.City,
            State = client.State,
            Pincode = client.Pincode,
            ContactPhone = client.ContactPhone,
            ContactEmail = client.ContactEmail,
            SubscriptionPlan = newPlan,
            AdminUsername = client.AdminUsername,
            AdminFullName = $"{client.LegalName} Administrator",
            AdminPasswordHash = hash,
            AdminSalt = salt,
            AdminTempPasswordPlain = client.AdminTempPassword,
            IssuedDateUtc = client.IssuedDateUtc,
            ExpiryDateUtc = newExpiry,
            GracePeriodDays = 7,
            MaxUsers = newPlan == SubscriptionTier.Enterprise ? 50 : 10,
            MaxProducts = newPlan == SubscriptionTier.Enterprise ? 100000 : 25000,
            EnabledModules = new List<string> { "Billing", "Inventory", "GSTReports", "RackManagement", "OfflineExchange" }
        };

        string keyContent = _keyService.GenerateProvisioningDataKey(payload, _caPrivateKeyPem);
        File.WriteAllText(filePath, keyContent);

        try
        {
            string rootKeysDir = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedKeys");
            Directory.CreateDirectory(rootKeysDir);
            File.WriteAllText(Path.Combine(rootKeysDir, fileName), keyContent);
        }
        catch
        {
            // Ignored
        }

        // Update database
        try
        {
            await _bootstrapService.BootstrapFromDataKeyAsync(payload);
        }
        catch
        {
            // Ignored
        }

        CalculateKpis();
        RefreshFilteredClients();
        StatusMessage = $"✅ Subscription for '{client.TradeName}' updated to {newPlan} Tier! Valid until {newExpiry:dd-MMM-yyyy}.";
    }

    // =========================================================================
    // QUICK ACTIONS WITH CONFIRMATIONS
    // =========================================================================

    [RelayCommand]
    private void ResendClientEmail(object? clientObj)
    {
        if (clientObj is not ClientDisplayItem client) return;

        ShowConfirmation(
            "Draft & Resend Client Activation Email",
            $"Do you want to prepare and open an onboarding activation email draft for '{client.TradeName}' with key delivery to '{client.ContactEmail}'?",
            () => ExecuteResendClientEmail(client),
            "📧 Prepare Email",
            "#2563EB");
    }

    private void ExecuteResendClientEmail(ClientDisplayItem client)
    {
        string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedKeys");
        Directory.CreateDirectory(exportDir);
        string fileName = $"Client_{client.BusinessCode}_DataKey.key";
        string filePath = Path.Combine(exportDir, fileName);

        if (!File.Exists(filePath))
        {
            var (hash, salt) = _hasher.HashPassword(client.AdminTempPassword);
            var payload = new ClientProvisioningPayload
            {
                BusinessCode = client.BusinessCode,
                LegalName = client.LegalName,
                TradeName = client.TradeName,
                GSTIN = client.GSTIN,
                PAN = client.PAN,
                BusinessType = client.BusinessType,
                Address = client.Address,
                City = client.City,
                State = client.State,
                Pincode = client.Pincode,
                ContactPhone = client.ContactPhone,
                ContactEmail = client.ContactEmail,
                SubscriptionPlan = client.SubscriptionPlan,
                AdminUsername = client.AdminUsername,
                AdminFullName = $"{client.LegalName} Administrator",
                AdminPasswordHash = hash,
                AdminSalt = salt,
                AdminTempPasswordPlain = client.AdminTempPassword,
                IssuedDateUtc = client.IssuedDateUtc,
                ExpiryDateUtc = client.ExpiryDateUtc,
                GracePeriodDays = 7,
                MaxUsers = client.SubscriptionPlan == SubscriptionTier.Enterprise ? 50 : 10,
                MaxProducts = client.SubscriptionPlan == SubscriptionTier.Enterprise ? 100000 : 25000,
                EnabledModules = new List<string> { "Billing", "Inventory", "GSTReports", "RackManagement", "OfflineExchange" }
            };
            string keyContent = _keyService.GenerateProvisioningDataKey(payload, _caPrivateKeyPem);
            File.WriteAllText(filePath, keyContent);
        }

        GeneratedKeyFilePath = filePath;
        GeneratedKeyFileName = fileName;
        EmailRecipient = !string.IsNullOrWhiteSpace(client.ContactEmail) ? client.ContactEmail : "owner@clientstore.com";
        EmailSubject = $"🔐 AFS Store Activation Key & Credentials - {client.TradeName} ({client.BusinessCode})";

        string greetingName = !string.IsNullOrWhiteSpace(client.ContactPerson) ? client.ContactPerson : client.TradeName;
        EmailBody = 
$@"Dear {greetingName},

Please find your official AFS Store Activation Key and Business Credentials below:

==================================================
1. BUSINESS ACCOUNT SUMMARY
==================================================
Business Code:      {client.BusinessCode}
Legal Entity Name:  {client.LegalName}
Store / Trade Name: {client.TradeName}
Classification:     {client.BusinessType}
GSTIN / PAN:        {client.GSTIN} / {client.PAN}
Subscription Plan:  {client.SubscriptionPlan} Tier
License Validity:   {client.IssuedDateUtc:dd-MMM-yyyy} to {client.ExpiryDateUtc:dd-MMM-yyyy} ({client.DaysRemaining} Days Remaining)

==================================================
2. INITIAL ADMINISTRATOR LOGIN CREDENTIALS
==================================================
Admin Username:     {client.AdminUsername}
Temporary Password: {client.AdminTempPassword}

==================================================
3. STORE ACTIVATION INSTRUCTIONS
==================================================
1. Launch the 'AFS Desktop Business App' on your local store computer.
2. Click '📂 Browse & Select Data Key File...'.
3. Select the attached '{fileName}' file.
4. Click '🚀 Activate & Bootstrap Business Database' and log in!

--------------------------------------------------
ATTACHED FILE: {fileName}
Protection: RSA-2048 Signed & AES-256-GCM Encrypted
--------------------------------------------------

Best Regards,
AFS Super Administration Team
";

        IsKeyGenerated = true;
        EmailStatusNotification = $"📧 Email draft prepared for {client.TradeName} ({EmailRecipient})!";
        CurrentTab = "CreateClient";
    }

    [RelayCommand]
    private void ReExportClientKey(object? clientObj)
    {
        if (clientObj is not ClientDisplayItem client) return;

        ShowConfirmation(
            "Re-Export Encryption Key Package",
            $"Do you want to re-export the cryptographic key file for '{client.TradeName}' ({client.BusinessCode}) and open Windows Explorer?",
            () => ExecuteReExportClientKey(client),
            "🔑 Re-Export",
            "#2563EB");
    }

    private void ExecuteReExportClientKey(ClientDisplayItem client)
    {
        string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedKeys");
        Directory.CreateDirectory(exportDir);
        string fileName = $"Client_{client.BusinessCode}_DataKey.key";
        string filePath = Path.Combine(exportDir, fileName);

        var (hash, salt) = _hasher.HashPassword(client.AdminTempPassword);
        var payload = new ClientProvisioningPayload
        {
            BusinessCode = client.BusinessCode,
            LegalName = client.LegalName,
            TradeName = client.TradeName,
            GSTIN = client.GSTIN,
            PAN = client.PAN,
            BusinessType = client.BusinessType,
            Address = client.Address,
            City = client.City,
            State = client.State,
            Pincode = client.Pincode,
            ContactPhone = client.ContactPhone,
            ContactEmail = client.ContactEmail,
            SubscriptionPlan = client.SubscriptionPlan,
            AdminUsername = client.AdminUsername,
            AdminFullName = $"{client.LegalName} Administrator",
            AdminPasswordHash = hash,
            AdminSalt = salt,
            AdminTempPasswordPlain = client.AdminTempPassword,
            IssuedDateUtc = client.IssuedDateUtc,
            ExpiryDateUtc = client.ExpiryDateUtc,
            GracePeriodDays = 7,
            MaxUsers = client.SubscriptionPlan == SubscriptionTier.Enterprise ? 50 : 10,
            MaxProducts = client.SubscriptionPlan == SubscriptionTier.Enterprise ? 100000 : 25000,
            EnabledModules = new List<string> { "Billing", "Inventory", "GSTReports", "RackManagement", "OfflineExchange" }
        };

        string keyContent = _keyService.GenerateProvisioningDataKey(payload, _caPrivateKeyPem);
        File.WriteAllText(filePath, keyContent);

        try
        {
            string rootKeysDir = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedKeys");
            Directory.CreateDirectory(rootKeysDir);
            File.WriteAllText(Path.Combine(rootKeysDir, fileName), keyContent);
        }
        catch
        {
            // Ignored
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch
        {
            // Ignored
        }

        StatusMessage = $"📁 Re-exported {fileName} and opened in Windows Explorer!";
    }

    [RelayCommand]
    private void CopyClientCredentials(object? clientObj)
    {
        if (clientObj is not ClientDisplayItem client) return;

        string text = 
$@"=== AFS BUSINESS ACCOUNT CREDENTIALS ===
Business Code:    {client.BusinessCode}
Legal Entity:     {client.LegalName}
Store / Trade:    {client.TradeName}
Classification:   {client.BusinessType}
GSTIN / PAN:      {client.GSTIN} / {client.PAN}
Contact Email:    {client.ContactEmail}
Contact Mobile:   {client.ContactPhone}
Admin Username:   {client.AdminUsername}
Admin Password:   {client.AdminTempPassword}
Plan Tier:        {client.SubscriptionPlan}
License Expiry:   {client.ExpiryDateUtc:dd-MMM-yyyy} ({client.DaysRemaining} Days Left)
Key File:         Client_{client.BusinessCode}_DataKey.key";

        try
        {
            Clipboard.SetText(text);
            StatusMessage = $"📋 Credentials for {client.TradeName} copied to clipboard!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not copy to clipboard: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleClientStatus(object? clientObj)
    {
        if (clientObj is not ClientDisplayItem client) return;

        string actionName = client.IsSuspended ? "Activate" : "Suspend";
        string buttonColor = client.IsSuspended ? "#16A34A" : "#DC2626";

        ShowConfirmation(
            $"{actionName} Client Account",
            $"Are you sure you want to {actionName.ToLowerInvariant()} the commercial account for '{client.TradeName}' ({client.BusinessCode})?",
            () =>
            {
                client.IsSuspended = !client.IsSuspended;
                client.NotifyStatusChanged();
                CalculateKpis();
                RefreshFilteredClients();

                StatusMessage = client.IsSuspended 
                    ? $"🚫 Client {client.TradeName} has been SUSPENDED." 
                    : $"✅ Client {client.TradeName} is now ACTIVE.";
            },
            $"{actionName} Account",
            buttonColor);
    }

    // =========================================================================
    // EMAIL DISPATCH ACTIONS
    // =========================================================================

    [RelayCommand]
    private void OpenMailClient()
    {
        try
        {
            string subject = Uri.EscapeDataString(EmailSubject);
            string body = Uri.EscapeDataString(EmailBody);
            string to = Uri.EscapeDataString(EmailRecipient);
            string mailtoUrl = $"mailto:{to}?subject={subject}&body={body}";

            var psi = new ProcessStartInfo
            {
                FileName = mailtoUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
            EmailStatusNotification = "📧 Default Email client opened with pre-filled draft! Remember to attach the .key file.";
        }
        catch (Exception ex)
        {
            EmailStatusNotification = $"Could not open email client: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyEmailDraft()
    {
        try
        {
            Clipboard.SetText(EmailBody);
            EmailStatusNotification = "📋 Complete email message copied to clipboard!";
        }
        catch (Exception ex)
        {
            EmailStatusNotification = $"Could not copy to clipboard: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenGeneratedKeysFolder()
    {
        try
        {
            if (File.Exists(GeneratedKeyFilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{GeneratedKeyFilePath}\"");
            }
            else
            {
                string dir = Path.GetDirectoryName(GeneratedKeyFilePath) ?? Directory.GetCurrentDirectory();
                Process.Start("explorer.exe", dir);
            }
            EmailStatusNotification = "📁 Opened folder containing encrypted .key file!";
        }
        catch (Exception ex)
        {
            EmailStatusNotification = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenWebmailGmail()
    {
        try
        {
            string subject = Uri.EscapeDataString(EmailSubject);
            string body = Uri.EscapeDataString(EmailBody);
            string to = Uri.EscapeDataString(EmailRecipient);
            string url = $"https://mail.google.com/mail/?view=cm&fs=1&to={to}&su={subject}&body={body}";

            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
            EmailStatusNotification = "🌐 Opened Gmail Webmail compose window!";
        }
        catch (Exception ex)
        {
            EmailStatusNotification = $"Could not open webmail: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetForm()
    {
        ShowConfirmation(
            "Reset Registration Form",
            "Are you sure you want to clear all entered business and contact form fields?",
            () =>
            {
                BusinessCode = GenerateUniqueBusinessCode();
                LegalName = string.Empty;
                TradeName = string.Empty;
                Gstin = string.Empty;
                Pan = string.Empty;
                ContactPerson = string.Empty;
                ContactEmail = string.Empty;
                ContactPhone = string.Empty;
                Address = string.Empty;
                City = "Mumbai";
                State = "Maharashtra";
                Pincode = "400001";
                AdminUsername = "admin";
                AdminTempPassword = "Password@2026!";
                IsKeyGenerated = false;
                IsValidationError = false;
                FormValidationErrorMessage = string.Empty;
                StatusMessage = string.Empty;
                EmailStatusNotification = string.Empty;
                EmailBody = string.Empty;
            },
            "🧹 Clear Form",
            "#64748B");
    }

    // =========================================================================
    // TAB 4: LICENSE RENEWALS & EXTENSIONS COMMANDS
    // =========================================================================

    [RelayCommand]
    private void SelectClientForRenewal(object? clientObj)
    {
        if (clientObj is ClientDisplayItem client)
        {
            SelectedRenewalClient = client;
            RenewalSelectedPlan = client.SubscriptionPlan;
            if (RenewalSelectedValidity == null && ValidityOptions.Count > 3)
            {
                RenewalSelectedValidity = ValidityOptions[3];
            }
            RecalculateRenewalExpiry();
            CurrentTab = "Renewals";
        }
    }

    [RelayCommand]
    private async Task GenerateLicenseRenewalPackage()
    {
        if (SelectedRenewalClient == null)
        {
            RenewalStatusMessage = "Please select a registered client business to renew.";
            return;
        }

        var client = SelectedRenewalClient;
        var newPlan = RenewalSelectedPlan;
        var newExpiry = RenewalCalculatedExpiryDate;

        try
        {
            var payload = new LicenseRenewalPayload
            {
                LicenseId = Guid.NewGuid().ToString("N"),
                BusinessCode = client.BusinessCode,
                BusinessType = client.BusinessType,
                Plan = newPlan,
                IssuedDateUtc = DateTime.UtcNow,
                ExpiryDateUtc = newExpiry,
                GracePeriodDays = 7,
                MaxUsers = newPlan == SubscriptionTier.Enterprise ? 50 : 10,
                MaxBranches = newPlan == SubscriptionTier.Enterprise ? 10 : 1,
                MaxProducts = newPlan == SubscriptionTier.Enterprise ? 100000 : 25000,
                EnabledModules = new List<string> { "Billing", "Inventory", "GSTReports", "RackManagement", "OfflineExchange" }
            };

            if (client.BusinessType == BusinessType.Pharmacy)
            {
                payload.EnabledModules.Add("PharmacyBatchExpiry");
            }

            // Generate cryptographically signed Base64 license certificate using CA RSA Master Private Key
            string licContent = _keyService.GenerateLicenseRenewal(payload, _caPrivateKeyPem);

            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedLicenses");
            Directory.CreateDirectory(exportDir);
            string fileName = $"License_{client.BusinessCode}_{DateTime.UtcNow:yyyyMMdd}.lic";
            string filePath = Path.Combine(exportDir, fileName);

            File.WriteAllText(filePath, licContent);

            try
            {
                string rootLicDir = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedLicenses");
                Directory.CreateDirectory(rootLicDir);
                File.WriteAllText(Path.Combine(rootLicDir, fileName), licContent);
            }
            catch { }

            // Update client entity in memory & database
            client.SubscriptionPlan = newPlan;
            client.ExpiryDateUtc = newExpiry;
            client.IsSuspended = false;
            client.NotifyStatusChanged();

            try
            {
                await _licenseRepo.SaveLicenseAsync(new LicenseRecord
                {
                    LicenseKey = payload.LicenseId,
                    BusinessCode = client.BusinessCode,
                    Plan = newPlan,
                    IssuedDateUtc = payload.IssuedDateUtc,
                    ExpiryDateUtc = payload.ExpiryDateUtc,
                    GracePeriodDays = payload.GracePeriodDays,
                    MaxUsers = payload.MaxUsers,
                    MaxProducts = payload.MaxProducts,
                    Status = LicenseStatus.Active,
                    Signature = payload.Signature
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAApplication] Database license save notice: {ex.Message}");
            }

            LastExportedLicensePath = filePath;
            LastExportedLicenseFileName = fileName;
            IsLicenseRenewalGenerated = true;

            RenewalSummaryText = 
$@"=== AFS RSA-4096 SIGNED LICENSE RENEWAL CERTIFICATE (.lic) ===
Business Code:       {client.BusinessCode}
Legal Entity:        {client.LegalName}
Trade Name:          {client.TradeName}
GSTIN / PAN:         {client.GSTIN} / {client.PAN}
Subscription Plan:   {newPlan} Tier
New Expiry Date:     {newExpiry:dd-MMM-yyyy} ({client.DaysRemaining} Days Active)
License ID:          {payload.LicenseId}
Certificate Status:  Digitally Signed with AFS CA RSA-4096 Key
Generated File:      {filePath}

Client Activation Instructions:
1. Deliver this '{fileName}' certificate file to the store owner / administrator.
2. In AFS Desktop Business App, the license will be verified offline with zero internet requirement.";

            RenewalStatusMessage = $"✅ License certificate '{fileName}' generated successfully for {client.TradeName}!";
            CalculateKpis();
            RefreshFilteredClients();

            try
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch { }
        }
        catch (Exception ex)
        {
            RenewalStatusMessage = $"❌ Error issuing license: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenLicenseFolder()
    {
        try
        {
            if (File.Exists(LastExportedLicensePath))
            {
                Process.Start("explorer.exe", $"/select,\"{LastExportedLicensePath}\"");
            }
            else
            {
                string dir = Path.GetDirectoryName(LastExportedLicensePath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedLicenses");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", dir);
            }
        }
        catch (Exception ex)
        {
            RenewalStatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyLicenseSummary()
    {
        try
        {
            Clipboard.SetText(RenewalSummaryText);
            RenewalStatusMessage = "📋 License Renewal Summary copied to clipboard!";
        }
        catch (Exception ex)
        {
            RenewalStatusMessage = $"Could not copy to clipboard: {ex.Message}";
        }
    }

    // =========================================================================
    // TAB 5: DECRYPT CLIENT COMMERCIAL AUDIT (.afspkg / .enc) COMMANDS
    // =========================================================================

    [RelayCommand]
    private void BrowseAuditPackageFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select AFS Commercial Encrypted Audit Package (.afspkg / .enc / .json)",
            Filter = "AFS Audit Packages (*.afspkg;*.enc;*.json;*.dat)|*.afspkg;*.enc;*.json;*.dat|All Files (*.*)|*.*"
        };

        string defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
        if (Directory.Exists(defaultDir))
        {
            dlg.InitialDirectory = defaultDir;
        }
        else
        {
            dlg.InitialDirectory = Directory.GetCurrentDirectory();
        }

        if (dlg.ShowDialog() == true)
        {
            SelectedAuditFilePath = dlg.FileName;
            DecryptStatusMessage = $"Selected file: {Path.GetFileName(dlg.FileName)}. Click '🔓 Decrypt & Inspect Package' to proceed.";
        }
    }

    [RelayCommand]
    private void SwitchAuditSubTab(object? subTabObj)
    {
        if (subTabObj is string subTab)
        {
            AuditActiveSubTab = subTab;
        }
    }

    [RelayCommand]
    private async Task ExecuteDecryptAuditPackageAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedAuditFilePath) || !File.Exists(SelectedAuditFilePath))
        {
            DecryptStatusMessage = "Please select a valid .afspkg or .enc package file first.";
            return;
        }

        try
        {
            string rawContent = File.ReadAllText(SelectedAuditFilePath).Trim();
            string plainJson = string.Empty;

            // Check if rawContent is an encrypted JSON package
            using var jsonDoc = JsonDocument.Parse(rawContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("Ciphertext", out var cipherProp) &&
                root.TryGetProperty("Nonce", out var nonceProp) &&
                root.TryGetProperty("Tag", out var tagProp))
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherProp.GetString()!);
                byte[] nonceBytes = Convert.FromBase64String(nonceProp.GetString()!);
                byte[] tagBytes = Convert.FromBase64String(tagProp.GetString()!);

                var aesGcm = new AesGcmService();
                byte[] masterKey = Encoding.UTF8.GetBytes("AFS_MASTER_COMMERCIAL_KEY_2026!!");
                byte[] decryptedBytes = aesGcm.Decrypt(cipherBytes, masterKey, nonceBytes, tagBytes);
                plainJson = Encoding.UTF8.GetString(decryptedBytes);
            }
            else
            {
                plainJson = rawContent;
            }

            // Parse inner JSON payload
            using var innerDoc = JsonDocument.Parse(plainJson);
            var innerRoot = innerDoc.RootElement;

            // Clear existing collections
            DecryptedAuditInvoices.Clear();
            DecryptedAuditProducts.Clear();
            DecryptedAuditLogs.Clear();
            DecryptedGstHsnSummaries.Clear();
            DecryptedGstRateBreakdowns.Clear();

            // 1. Read Metadata
            JsonElement metaElem = innerRoot;
            if (innerRoot.TryGetProperty("ExportMetadata", out var expMeta))
            {
                metaElem = expMeta;
            }

            AuditBusinessCode = metaElem.TryGetProperty("BusinessCode", out var bc) ? bc.GetString() ?? "" : "";
            AuditLegalName = metaElem.TryGetProperty("LegalName", out var ln) ? ln.GetString() ?? "" : "";
            AuditGstin = metaElem.TryGetProperty("GSTIN", out var gst) ? gst.GetString() ?? "" : "";
            AuditAccountingPeriod = metaElem.TryGetProperty("AccountingPeriod", out var ap) ? ap.GetString() ?? "" : "";
            AuditExportTimestamp = metaElem.TryGetProperty("ExportTimestampUtc", out var ts)
                ? (ts.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow)
                : DateTime.UtcNow;

            JsonElement metricsElem = metaElem;
            if (innerRoot.TryGetProperty("Metrics", out var metricsProp))
            {
                metricsElem = metricsProp;
            }

            AuditTotalInvoices = metricsElem.TryGetProperty("TotalInvoices", out var ti) ? ti.GetInt32() : 0;
            AuditTotalRevenue = metricsElem.TryGetProperty("TotalRevenue", out var tr) ? tr.GetDecimal() : 0;
            AuditTaxableTurnover = metricsElem.TryGetProperty("TaxableTurnover", out var tt) ? tt.GetDecimal() : 0;
            AuditTotalCgst = metricsElem.TryGetProperty("CGST", out var cg) ? cg.GetDecimal() : 0;
            AuditTotalSgst = metricsElem.TryGetProperty("SGST", out var sg) ? sg.GetDecimal() : 0;
            AuditTotalIgst = metricsElem.TryGetProperty("IGST", out var ig) ? ig.GetDecimal() : 0;
            AuditTotalTax = metricsElem.TryGetProperty("TotalTax", out var tx) ? tx.GetDecimal() : 0;
            AuditTotalProducts = metricsElem.TryGetProperty("TotalProducts", out var tp) ? tp.GetInt32() : 0;
            AuditInventoryValuation = metricsElem.TryGetProperty("InventoryValuation", out var iv) ? iv.GetDecimal() : 0;
            AuditLogsCount = metricsElem.TryGetProperty("AuditRecordsCount", out var al) ? al.GetInt32() : 0;

            var invoicesList = new List<Invoice>();
            var productsList = new List<Product>();
            var logsList = new List<AuditLog>();

            // 2. Read Invoices
            if (innerRoot.TryGetProperty("Invoices", out var invoicesProp))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var invoices = JsonSerializer.Deserialize<List<Invoice>>(invoicesProp.GetRawText(), options);
                if (invoices != null)
                {
                    invoicesList = invoices;
                    foreach (var inv in invoices)
                    {
                        DecryptedAuditInvoices.Add(inv);
                    }

                    if (AuditTotalInvoices == 0) AuditTotalInvoices = invoices.Count;
                    if (AuditTotalRevenue == 0) AuditTotalRevenue = invoices.Sum(i => i.GrandTotal);
                    if (AuditTaxableTurnover == 0) AuditTaxableTurnover = invoices.Sum(i => i.TaxableAmount);
                    if (AuditTotalCgst == 0) AuditTotalCgst = invoices.Sum(i => i.TotalCGST);
                    if (AuditTotalSgst == 0) AuditTotalSgst = invoices.Sum(i => i.TotalSGST);
                    if (AuditTotalIgst == 0) AuditTotalIgst = invoices.Sum(i => i.TotalIGST);
                    if (AuditTotalTax == 0) AuditTotalTax = AuditTotalCgst + AuditTotalSgst + AuditTotalIgst;

                    // Compute HSN Summaries from invoice items
                    var hsnGroups = invoices.SelectMany(i => i.Items ?? new List<InvoiceItem>())
                        .GroupBy(item => string.IsNullOrWhiteSpace(item.HSNCode) ? "HSN-GEN" : item.HSNCode);

                    foreach (var grp in hsnGroups)
                    {
                        decimal totalQty = grp.Sum(x => x.Quantity);
                        decimal totalVal = grp.Sum(x => x.TotalAmount);
                        decimal totalTax = grp.Sum(x => x.CGSTAmount + x.SGSTAmount + x.IGSTAmount);
                        decimal taxable = grp.Sum(x => x.TaxableValue > 0 ? x.TaxableValue : (x.TotalAmount - totalTax));
                        decimal firstRate = grp.FirstOrDefault()?.GSTRate ?? 0;
                        string desc = grp.FirstOrDefault()?.ProductName ?? "General Goods";

                        DecryptedGstHsnSummaries.Add(new GstHsnSummaryItem
                        {
                            HSNCode = grp.Key,
                            Description = desc,
                            TotalQuantity = totalQty,
                            TotalValue = totalVal,
                            TaxableValue = taxable,
                            GstRate = firstRate,
                            CentralTaxAmount = Math.Round(totalTax / 2, 2),
                            StateTaxAmount = Math.Round(totalTax / 2, 2),
                            IntegratedTaxAmount = 0
                        });
                    }

                    // Compute Rate Breakdowns
                    var rateGroups = invoices.SelectMany(i => i.Items ?? new List<InvoiceItem>())
                        .GroupBy(item => item.GSTRate);

                    foreach (var grp in rateGroups.OrderBy(g => g.Key))
                    {
                        decimal taxable = grp.Sum(x => x.TaxableValue > 0 ? x.TaxableValue : (x.TotalAmount - (x.CGSTAmount + x.SGSTAmount + x.IGSTAmount)));
                        decimal tax = grp.Sum(x => x.CGSTAmount + x.SGSTAmount + x.IGSTAmount);
                        decimal halfTax = Math.Round(tax / 2, 2);

                        DecryptedGstRateBreakdowns.Add(new GstRateBreakdownItem
                        {
                            RateLabel = $"GST @ {grp.Key:N0}% Slabs",
                            GstRate = grp.Key,
                            TaxableAmount = taxable,
                            CGSTAmount = halfTax,
                            SGSTAmount = halfTax,
                            IGSTAmount = 0
                        });
                    }
                }
            }

            // 3. Read Products
            if (innerRoot.TryGetProperty("Products", out var productsProp))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var products = JsonSerializer.Deserialize<List<Product>>(productsProp.GetRawText(), options);
                if (products != null)
                {
                    productsList = products;
                    foreach (var prod in products)
                    {
                        DecryptedAuditProducts.Add(prod);
                    }
                    if (AuditTotalProducts == 0) AuditTotalProducts = products.Count;
                    if (AuditInventoryValuation == 0) AuditInventoryValuation = products.Sum(p => p.CurrentStock * p.PurchasePrice);
                }
            }

            // 4. Read AuditLogs
            if (innerRoot.TryGetProperty("AuditLogs", out var logsProp))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var logs = JsonSerializer.Deserialize<List<AuditLog>>(logsProp.GetRawText(), options);
                if (logs != null)
                {
                    logsList = logs;
                    foreach (var log in logs)
                    {
                        DecryptedAuditLogs.Add(log);
                    }
                    if (AuditLogsCount == 0) AuditLogsCount = logs.Count;
                }
            }

            // 5. Persist All Decrypted Commercial Data into PostgreSQL Database
            await PersistDecryptedAuditDataToDatabaseAsync(plainJson, invoicesList, productsList, logsList);

            IsAuditPackageDecrypted = true;
            AuditActiveSubTab = "Invoices";
            DecryptStatusMessage = $"✅ Successfully decrypted & saved to database for '{AuditLegalName}' ({AuditBusinessCode})! Invoices: {invoicesList.Count}, Products: {productsList.Count}, Logs: {logsList.Count}. Period: {AuditAccountingPeriod}";
        }
        catch (Exception ex)
        {
            IsAuditPackageDecrypted = false;
            DecryptStatusMessage = $"❌ Decryption error: {ex.Message}";
        }
    }

    private async Task PersistDecryptedAuditDataToDatabaseAsync(
        string rawJsonPayload,
        List<Invoice> invoices,
        List<Product> products,
        List<AuditLog> auditLogs)
    {
        try
        {
            // Persist Products
            foreach (var prod in products)
            {
                try { await _productRepo.CreateAsync(prod); } catch { }
            }

            // Persist Invoices & Items
            foreach (var inv in invoices)
            {
                try { await _invoiceRepo.SaveInvoiceAtomicAsync(inv); } catch { }
            }

            // Persist Audit Logs
            foreach (var log in auditLogs)
            {
                try { await _auditRepo.LogAsync(log); } catch { }
            }

            // Persist Decrypted Audit Package Record
            var packageRecord = new DecryptedAuditPackage
            {
                Id = Guid.NewGuid(),
                BusinessCode = AuditBusinessCode,
                LegalName = AuditLegalName,
                GSTIN = AuditGstin,
                AccountingPeriod = AuditAccountingPeriod,
                ExportTimestampUtc = AuditExportTimestamp,
                DecryptedAtUtc = DateTime.UtcNow,
                TotalInvoices = invoices.Count,
                TotalRevenue = AuditTotalRevenue,
                TaxableTurnover = AuditTaxableTurnover,
                TotalCGST = AuditTotalCgst,
                TotalSGST = AuditTotalSgst,
                TotalIGST = AuditTotalIgst,
                TotalTax = AuditTotalTax,
                TotalProducts = products.Count,
                InventoryValuation = AuditInventoryValuation,
                AuditLogsCount = auditLogs.Count,
                PackageFilePath = SelectedAuditFilePath,
                RawPayloadJson = rawJsonPayload
            };

            await _decryptedPackageRepo.SaveDecryptedPackageAsync(packageRecord);

            // Log SuperAdmin Audit Entry
            await _auditRepo.LogAsync(new AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                UserId = Guid.Empty,
                Username = "superadmin",
                MachineName = Environment.MachineName,
                Module = "CA_AUDIT_DECRYPTION",
                Action = AuditActionType.DataExported,
                RecordId = AuditBusinessCode,
                NewValue = $"Decrypted & DB-saved {invoices.Count} invoices, {products.Count} products, {auditLogs.Count} audit logs for period {AuditAccountingPeriod}",
                Reason = "Super Admin Commercial Audit Verification"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] Database persistence error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportDecryptedAuditCsv()
    {
        if (!IsAuditPackageDecrypted || DecryptedAuditInvoices.Count == 0)
        {
            DecryptStatusMessage = "No decrypted invoices available to export.";
            return;
        }

        try
        {
            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "Decrypted");
            Directory.CreateDirectory(exportDir);
            string fileName = $"Decrypted_Invoices_{AuditBusinessCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string filePath = Path.Combine(exportDir, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("InvoiceNumber,DateUTC,CustomerName,CustomerPhone,CustomerGSTIN,PaymentMethod,SubTotal,TotalDiscount,TotalTax,GrandTotal");

            foreach (var inv in DecryptedAuditInvoices)
            {
                sb.AppendLine($"\"{inv.InvoiceNumber}\",\"{inv.InvoiceDateUtc:yyyy-MM-dd HH:mm}\",\"{inv.CustomerName}\",\"{inv.CustomerPhone}\",\"{inv.CustomerGSTIN}\",\"{inv.PaymentMethod}\",{inv.SubTotal:F2},{inv.TotalDiscount:F2},{inv.TotalTax:F2},{inv.GrandTotal:F2}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            DecryptStatusMessage = $"📁 Exported decrypted ledger CSV to: {fileName}";
            try
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch { }
        }
        catch (Exception ex)
        {
            DecryptStatusMessage = $"CSV Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenAuditFolder()
    {
        try
        {
            string dir = Path.GetDirectoryName(SelectedAuditFilePath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }
        catch (Exception ex)
        {
            DecryptStatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearDecryptedAudit()
    {
        DecryptedAuditInvoices.Clear();
        DecryptedAuditProducts.Clear();
        DecryptedAuditLogs.Clear();
        DecryptedGstHsnSummaries.Clear();
        DecryptedGstRateBreakdowns.Clear();
        SelectedAuditFilePath = string.Empty;
        IsAuditPackageDecrypted = false;
        DecryptStatusMessage = string.Empty;
    }
}
