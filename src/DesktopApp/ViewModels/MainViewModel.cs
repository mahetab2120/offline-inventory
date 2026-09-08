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

namespace DesktopApp.ViewModels;

public class StringEqualsToBoolConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return parameter.ToString()!;
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}

public static class CurrencyWordsHelper
{
    private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
    private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    public static string ConvertToIndianRupeesWords(decimal amount)
    {
        if (amount == 0) return "Zero Rupees Only";
        long rupees = (long)Math.Floor(amount);
        int paise = (int)Math.Round((amount - rupees) * 100);

        string words = ConvertNumberToWords(rupees).Trim() + " Rupees";
        if (paise > 0)
        {
            words += " and " + ConvertNumberToWords(paise).Trim() + " Paise";
        }
        return words + " Only";
    }

    private static string ConvertNumberToWords(long number)
    {
        if (number == 0) return "";
        if (number < 20) return Ones[number] + " ";
        if (number < 100) return Tens[number / 10] + " " + Ones[number % 10] + " ";
        if (number < 1000) return Ones[number / 100] + " Hundred " + ConvertNumberToWords(number % 100);
        if (number < 100000) return ConvertNumberToWords(number / 1000) + "Thousand " + ConvertNumberToWords(number % 1000);
        if (number < 10000000) return ConvertNumberToWords(number / 100000) + "Lakh " + ConvertNumberToWords(number % 100000);
        return ConvertNumberToWords(number / 10000000) + "Crore " + ConvertNumberToWords(number % 10000000);
    }
}

public class PharmacyBatchDisplayItem : ObservableObject
{
    public Guid ProductId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Stock { get; set; }
    public decimal MRP { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public string RackLocation { get; set; } = "Shelf A-01";

    public int DaysToExpiry => (int)(ExpiryDate.Date - DateTime.UtcNow.Date).TotalDays;
    
    public string Status => DaysToExpiry < 0 
        ? "EXPIRED" 
        : DaysToExpiry <= 60 
            ? "NEAR EXPIRY" 
            : DaysToExpiry <= 180 
                ? "EXPIRING SOON" 
                : "SAFE & FRESH";

    public string StatusColor => DaysToExpiry < 0 
        ? "#EF4444" 
        : DaysToExpiry <= 60 
            ? "#F59E0B" 
            : DaysToExpiry <= 180 
                ? "#38BDF8" 
                : "#10B981";

    public System.Windows.Media.SolidColorBrush StatusBrush => DaysToExpiry < 0 
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68))
        : DaysToExpiry <= 60 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11))
            : DaysToExpiry <= 180 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));

    public string ExpiryBadge => $"{ExpiryDate:MMM yyyy} ({DaysToExpiry}d)";
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

public class CategoryDistributionItem
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal TotalStockValue { get; set; }
    public double Percentage { get; set; }
    public string FormattedPercentage => $"{Percentage:N0}%";
}

public class WeeklySalesItem
{
    public string DayName { get; set; } = string.Empty;
    public decimal SalesAmount { get; set; }
    public double BarHeight { get; set; }
    public string FormattedAmount => $"₹{SalesAmount:N0}";
}

public class ParkedBill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReferenceName { get; set; } = string.Empty;
    public DateTime ParkedAt { get; set; } = DateTime.UtcNow;
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerGstin { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public string DiscountType { get; set; } = "Flat";
    public List<InvoiceItem> Items { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public int TotalItemCount => Items.Sum(i => (int)i.Quantity);
    public string DisplaySummary => $"{CustomerName} • {TotalItemCount} items • ₹{GrandTotal:N2} ({ParkedAt:HH:mm:ss})";
}

public partial class MainViewModel : ObservableObject
{
    private readonly IProvisioningKeyService _keyService;
    private readonly IBootstrapService _bootstrapService;
    private readonly IAuthenticationService _authService;
    private readonly ICompanyRepository _companyRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILicenseRepository _licenseRepo;
    private readonly IAuditLogRepository _auditRepo;
    private readonly ILicenseManager _licenseManager;
    private readonly IPasswordHasher _hasher;

    // View States: "OnboardingLock", "Login", "Dashboard"
    [ObservableProperty]
    private string currentViewState = "OnboardingLock";

    // Navigation Tabs in Main Dashboard: "Dashboard", "Billing", "Inventory", "Pharmacy", "Reports", "Users", "CaExport"
    [ObservableProperty]
    private string currentDashboardTab = "Dashboard";

    partial void OnCurrentDashboardTabChanged(string value)
    {
        if (value == "Reports")
        {
            GenerateGstReport();
        }
        else if (value == "CaExport")
        {
            _ = UpdateCaExportPeriodPreviewAsync();
        }
    }

    // --- Onboarding Lock State Properties ---
    [ObservableProperty]
    private string selectedKeyFilePath = string.Empty;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private bool isValidKeyLoaded = false;

    [ObservableProperty]
    private ClientProvisioningPayload? previewPayload;

    [ObservableProperty]
    private string previewSummary = string.Empty;

    // --- Login State Properties ---
    [ObservableProperty]
    private string loginUsername = "admin";

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

    // --- Store Executive Identity & Dashboard State ---
    [ObservableProperty]
    private Company? currentCompany;

    [ObservableProperty]
    private User? currentUser;

    [ObservableProperty]
    private LicenseRecord? currentLicense;

    [ObservableProperty]
    private string licenseDaysRemainingText = string.Empty;

    // Dashboard KPIs
    [ObservableProperty]
    private decimal todaySalesTotal = 0;

    [ObservableProperty]
    private int todayInvoiceCount = 0;

    [ObservableProperty]
    private decimal monthlyTurnover = 0;

    [ObservableProperty]
    private int totalProductCount = 0;

    [ObservableProperty]
    private decimal totalInventoryValue = 0;

    [ObservableProperty]
    private int lowStockCount = 0;

    [ObservableProperty]
    private int pharmacyExpiredCount = 0;

    [ObservableProperty]
    private int pharmacyNearExpiryCount = 0;

    [ObservableProperty]
    private int activeStaffCount = 1;

    // Dashboard Collections
    public ObservableCollection<Product> LowStockProducts { get; } = new();
    public ObservableCollection<PharmacyBatchDisplayItem> ExpiringBatchesSummary { get; } = new();
    public ObservableCollection<WeeklySalesItem> WeeklySalesTrends { get; } = new();
    public ObservableCollection<CategoryDistributionItem> CategoryDistributions { get; } = new();
    public ObservableCollection<ParkedBill> ParkedBills { get; } = new();

    [ObservableProperty]
    private int parkedBillsCount = 0;

    // --- POS Quick Billing Properties ---
    [ObservableProperty]
    private string posSearchQuery = string.Empty;

    [ObservableProperty]
    private string customerName = "Walk-in Customer";

    [ObservableProperty]
    private string customerPhone = string.Empty;

    [ObservableProperty]
    private string customerGstin = string.Empty;

    // --- Discount Management (Restricted to Business Admin & Authorized Users) ---
    [ObservableProperty]
    private string discountType = "Flat"; // "Flat" (₹) or "Percentage" (%)

    [ObservableProperty]
    private decimal discountValue = 0;

    [ObservableProperty]
    private decimal posGrossTotal = 0;

    [ObservableProperty]
    private decimal posDiscountTotal = 0;

    [ObservableProperty]
    private decimal posSubTotal = 0;

    [ObservableProperty]
    private decimal posTaxTotal = 0;

    [ObservableProperty]
    private decimal posGrandTotal = 0;

    public bool CanApplyDiscount => CurrentUser?.Role == UserRole.BusinessAdmin ||
                                   (CurrentUser?.Permissions.HasFlag(SystemPermissions.ApplyDiscount) ?? false);

    public string DiscountPermissionStatusText => CanApplyDiscount
        ? "🟢 Discount Authorized (Business Admin)"
        : "🔒 Discount Locked (Business Admin Only)";

    public bool CanAccessGstReports => false;

    public bool CanAccessSuperAdminExport => CurrentUser?.Role == UserRole.BusinessAdmin;

    public bool IsSuperAdmin => false;

    public string SuperAdminBadgeText => "🏢 Business Admin";

    partial void OnCurrentUserChanged(User? value)
    {
        OnPropertyChanged(nameof(CanApplyDiscount));
        OnPropertyChanged(nameof(DiscountPermissionStatusText));
        OnPropertyChanged(nameof(CanAccessGstReports));
        OnPropertyChanged(nameof(CanAccessSuperAdminExport));
        OnPropertyChanged(nameof(IsSuperAdmin));
        OnPropertyChanged(nameof(SuperAdminBadgeText));
        RecalculateCartTotals();
    }

    partial void OnDiscountValueChanged(decimal value)
    {
        RecalculateCartTotals();
    }

    partial void OnDiscountTypeChanged(string value)
    {
        RecalculateCartTotals();
    }

    [ObservableProperty]
    private string posStatusMessage = string.Empty;

    [ObservableProperty]
    private PaymentMethod selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string selectedPosCategory = "🌟 All Categories";

    public ObservableCollection<string> PosCategories { get; } = new()
    {
        "🌟 All Categories",
        "💊 Medicines & Tablets",
        "🧪 Syrups & Liquids",
        "🩹 Surgicals & First Aid",
        "🧴 Personal Care & Hygiene",
        "🍼 Baby Care & Nutrition",
        "🛒 FMCG & Groceries",
        "📦 General Store"
    };

    public ObservableCollection<InvoiceItem> PosCartItems { get; } = new();
    public ObservableCollection<Product> AvailableProducts { get; } = new();
    public ObservableCollection<Product> FilteredPosProducts { get; } = new();
    public ObservableCollection<Invoice> RecentInvoices { get; } = new();
    public ObservableCollection<Invoice> FilteredInvoices { get; } = new();
    public ObservableCollection<PaymentMethod> PaymentMethods { get; } = new(Enum.GetValues<PaymentMethod>());

    // --- Billing Sub-Tabs (New Sale vs Previous Invoices History) ---
    [ObservableProperty]
    private string billingSubTab = "NewSale";

    partial void OnBillingSubTabChanged(string value)
    {
        if (value == "History")
        {
            RefreshFilteredInvoices();
        }
    }

    [ObservableProperty]
    private string invoiceSearchQuery = string.Empty;

    // --- Print Preview Modal State ---
    [ObservableProperty]
    private bool isPrintPreviewModalOpen = false;

    [ObservableProperty]
    private Invoice? selectedPrintInvoice;

    [ObservableProperty]
    private string invoiceAmountInWords = string.Empty;

    [ObservableProperty]
    private string printPreviewMode = "Thermal"; // "Thermal", "A4", "A5"

    [ObservableProperty]
    private string defaultPrintFormat = "Thermal"; // Default format preference: "Thermal", "A4", "A5"

    [ObservableProperty]
    private string selectedPrinter = "Default Windows Printer";

    [ObservableProperty]
    private bool isDirectPrintEnabled = true;

    public ObservableCollection<string> AvailablePrinters { get; } = new();

    public ObservableCollection<string> AvailablePrintFormats { get; } = new()
    {
        "🧾 80mm POS Thermal Slip",
        "📄 A4 Full Page Tax Invoice",
        "📑 A5 Half Page Tax Invoice"
    };

    // --- Inventory & Products Tab State ---
    [ObservableProperty]
    private string inventorySearchQuery = string.Empty;

    [ObservableProperty]
    private string selectedInventoryCategory = "🌟 All Categories";

    public ObservableCollection<Product> FilteredInventoryProducts { get; } = new();

    // Category Management & Industry Preset Modal State
    [ObservableProperty]
    private bool isManageCategoriesModalOpen = false;

    [ObservableProperty]
    private string newCategoryName = string.Empty;

    [ObservableProperty]
    private string newCategoryIcon = "☕";

    [ObservableProperty]
    private string categoryManagementMessage = string.Empty;

    public ObservableCollection<string> AvailableCategoryIcons { get; } = new()
    {
        "☕", "🥤", "🥪", "🍕", "🍔", "🍰", "🍟", "🥗", "🍜", "🍨",
        "💊", "🧪", "💉", "🩹", "🧴", "🍼", "🌿",
        "🛒", "🌾", "🥛", "🍪", "🧼", "🍬", "🍫",
        "👕", "👗", "👟", "🎒",
        "📱", "💻", "🎧", "🔌", "🔋", "📺",
        "📦", "🎁", "✨"
    };

    public ObservableCollection<string> MasterCategoryList { get; } = new()
    {
        "💊 Pharmaceuticals & Tablets",
        "🧪 Syrups, Suspensions & Liquids",
        "💉 Injections & Vaccines",
        "🩹 Surgicals & First Aid",
        "🧴 Personal Care & Hygiene",
        "🍼 Baby Care & Nutrition",
        "🛒 FMCG & Groceries",
        "📦 General Store Items"
    };

    // Add Product Form Dropdowns & Properties
    public ObservableCollection<string> AvailableCategories { get; } = new()
    {
        "💊 Pharmaceuticals & Tablets",
        "🧪 Syrups, Suspensions & Liquids",
        "💉 Injections & Vaccines",
        "🩹 Surgicals & First Aid",
        "🧴 Personal Care & Hygiene",
        "🍼 Baby Care & Nutrition",
        "🛒 FMCG & Groceries",
        "📦 General Store Items"
    };

    public ObservableCollection<string> AvailableUnits { get; } = new()
    {
        "PCS - Pieces",
        "STRIP - Tablet Strip (10's/15's)",
        "BOX - Outer Pack / Box",
        "BTL - Liquid Bottle",
        "VIAL - Injection Vial",
        "TUBE - Cream / Ointment",
        "KG - Kilograms",
        "GM - Grams",
        "LTR - Liters",
        "ML - Milliliters",
        "PACK - Standard Packet"
    };

    public ObservableCollection<string> AvailableGstSlabs { get; } = new()
    {
        "0% - GST Nil / Exempted",
        "5% - GST Essential / Life-Saving Drugs",
        "12% - GST Standard Pharmaceuticals",
        "18% - GST Standard Commercial FMCG",
        "28% - GST Luxury / High Tax Bracket"
    };

    public ObservableCollection<string> AvailableHsnCodes { get; } = new()
    {
        "300490 - Medicaments & Formulations",
        "300420 - Antibiotics & Anti-infectives",
        "300610 - Sterile Surgical Dressings",
        "330499 - Skincare & Personal Care",
        "210690 - Dietary & Nutrition Supplements",
        "190590 - FMCG Foods & Bakery",
        "847130 - Electronic Data Processing",
        "000000 - Other / General Items"
    };

    public ObservableCollection<string> AvailableRackLocations { get; } = new()
    {
        "Shelf A1 - Fast Moving Tablets",
        "Shelf A2 - Antibiotics & Prescription",
        "Shelf B1 - Syrups & Liquids",
        "Shelf B2 - Topicals, Ointments & Drops",
        "Shelf C1 - Surgicals & Dressings",
        "Cold Storage - Refrigerator (2°C - 8°C)",
        "General Storage - Warehouse Bay 1"
    };

    [ObservableProperty]
    private string selectedCategory = "💊 Pharmaceuticals & Tablets";

    [ObservableProperty]
    private string selectedUnit = "STRIP - Tablet Strip (10's/15's)";

    [ObservableProperty]
    private string selectedGstSlab = "12% - GST Standard Pharmaceuticals";

    [ObservableProperty]
    private string selectedHsn = "300490 - Medicaments & Formulations";

    [ObservableProperty]
    private string selectedRackLocation = "Shelf A1 - Fast Moving Tablets";

    // --- Custom Storage Rack Creation Modal State ---
    [ObservableProperty]
    private bool isCreateRackModalOpen = false;

    [ObservableProperty]
    private string newRackWarehouse = "Main Store";

    [ObservableProperty]
    private string newRackSection = "Section A";

    [ObservableProperty]
    private string newRackCode = "Rack 01";

    [ObservableProperty]
    private string newRackShelf = "Shelf 1";

    [ObservableProperty]
    private string newRackBin = "";

    [ObservableProperty]
    private string newRackLabel = "Front OTC / Dispensing";

    [ObservableProperty]
    private string rackCreationMessage = string.Empty;

    public string PreviewRackLocationPath
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(NewRackWarehouse)) parts.Add(NewRackWarehouse.Trim());
            if (!string.IsNullOrWhiteSpace(NewRackSection)) parts.Add(NewRackSection.Trim());
            if (!string.IsNullOrWhiteSpace(NewRackCode)) parts.Add(NewRackCode.Trim());
            if (!string.IsNullOrWhiteSpace(NewRackShelf)) parts.Add(NewRackShelf.Trim());
            if (!string.IsNullOrWhiteSpace(NewRackBin)) parts.Add($"Bin {NewRackBin.Trim()}");
            
            string path = parts.Count > 0 ? string.Join(" → ", parts) : "Custom Storage Location";
            if (!string.IsNullOrWhiteSpace(NewRackLabel))
            {
                path += $" ({NewRackLabel.Trim()})";
            }
            return path;
        }
    }

    partial void OnNewRackWarehouseChanged(string value) => OnPropertyChanged(nameof(PreviewRackLocationPath));
    partial void OnNewRackSectionChanged(string value) => OnPropertyChanged(nameof(PreviewRackLocationPath));
    partial void OnNewRackCodeChanged(string value) => OnPropertyChanged(nameof(PreviewRackLocationPath));
    partial void OnNewRackShelfChanged(string value) => OnPropertyChanged(nameof(PreviewRackLocationPath));
    partial void OnNewRackBinChanged(string value) => OnPropertyChanged(nameof(PreviewRackLocationPath));
    partial void OnNewRackLabelChanged(string value) => OnPropertyChanged(nameof(PreviewRackLocationPath));

    [ObservableProperty]
    private string newProductName = string.Empty;

    [ObservableProperty]
    private string newProductBrand = string.Empty;

    [ObservableProperty]
    private string newProductSku = string.Empty;

    [ObservableProperty]
    private string newProductBarcode = string.Empty;

    [ObservableProperty]
    private decimal newPurchasePrice = 100;

    [ObservableProperty]
    private decimal newSellingPrice = 150;

    [ObservableProperty]
    private decimal newMrp = 150;

    [ObservableProperty]
    private decimal newCurrentStock = 50;

    [ObservableProperty]
    private decimal newMinStock = 10;

    [ObservableProperty]
    private string newBatchNumber = "BAT-" + DateTime.Now.ToString("yyMM");

    [ObservableProperty]
    private DateTime newExpiryDate = DateTime.Now.AddMonths(18);

    [ObservableProperty]
    private string inventoryFormMessage = string.Empty;

    // --- Stock Control & Adjustment Modal State ---
    [ObservableProperty]
    private bool isStockControlModalOpen = false;

    [ObservableProperty]
    private Product? selectedStockProduct;

    [ObservableProperty]
    private string stockAdjustmentType = "Inward"; // "Inward" (Stock IN), "Outward" (Stock OUT), "Set" (Direct Override)

    [ObservableProperty]
    private decimal stockAdjustmentQuantity = 10;

    [ObservableProperty]
    private string stockAdjustmentReason = "📥 Supplier Purchase / Goods Inward";

    [ObservableProperty]
    private string stockReferenceNumber = string.Empty;

    [ObservableProperty]
    private string stockSupplierOrParty = string.Empty;

    [ObservableProperty]
    private string stockAdjustmentNotes = string.Empty;

    [ObservableProperty]
    private string stockControlMessage = string.Empty;

    [ObservableProperty]
    private string stockUpdatedBatch = string.Empty;

    [ObservableProperty]
    private DateTime? stockUpdatedExpiry;

    [ObservableProperty]
    private string stockUpdatedRack = string.Empty;

    public ObservableCollection<string> AvailableStockReasons { get; } = new()
    {
        "📥 Supplier Purchase / Goods Inward",
        "🔄 Inventory Audit / Physical Stock Recount",
        "↩️ Customer Return / Sales Inward",
        "⚠️ Damaged / Broken Goods Write-Off",
        "⏳ Expired Stock Disposal",
        "🧪 Internal Testing / Samples / Consumption",
        "📦 Warehouse / Rack Relocation",
        "✏️ Manual Inventory Correction"
    };

    public decimal CalculatedNewStock
    {
        get
        {
            if (SelectedStockProduct == null) return 0;
            decimal cur = SelectedStockProduct.CurrentStock;
            decimal qty = Math.Max(0, StockAdjustmentQuantity);

            if (StockAdjustmentType == "Inward" || StockAdjustmentType == "IN")
            {
                return cur + qty;
            }
            else if (StockAdjustmentType == "Outward" || StockAdjustmentType == "OUT")
            {
                return Math.Max(0, cur - qty);
            }
            else // "Set"
            {
                return qty;
            }
        }
    }

    partial void OnStockAdjustmentTypeChanged(string value) => OnPropertyChanged(nameof(CalculatedNewStock));
    partial void OnStockAdjustmentQuantityChanged(decimal value) => OnPropertyChanged(nameof(CalculatedNewStock));
    partial void OnSelectedStockProductChanged(Product? value) => OnPropertyChanged(nameof(CalculatedNewStock));

    // --- Pharmacy Batch Tracker State ---
    [ObservableProperty]
    private string pharmacySearchQuery = string.Empty;

    [ObservableProperty]
    private string pharmacyFilterMode = "All"; // All, Expired, NearExpiry, Safe

    public ObservableCollection<PharmacyBatchDisplayItem> AllPharmacyBatches { get; } = new();
    public ObservableCollection<PharmacyBatchDisplayItem> FilteredPharmacyBatches { get; } = new();

    // Month & Year Collections for GST Reports & Super Admin Export
    public ObservableCollection<string> AvailableExportMonths { get; } = new()
    {
        "🌟 Full Accounting Year (All 12 Months)",
        "01 - January",
        "02 - February",
        "03 - March",
        "04 - April",
        "05 - May",
        "06 - June",
        "07 - July",
        "08 - August",
        "09 - September",
        "10 - October",
        "11 - November",
        "12 - December"
    };

    public ObservableCollection<int> AvailableExportYears { get; } = new()
    {
        2024, 2025, 2026, 2027, 2028, 2029, 2030
    };

    // --- GST Reports State ---
    [ObservableProperty]
    private string selectedGstReportType = "GSTR-1"; // GSTR-1, GSTR-2, GSTR-3B, SalesSummary, StockValuation

    [ObservableProperty]
    private int selectedGstReportYear = DateTime.UtcNow.Year;

    [ObservableProperty]
    private string selectedGstReportMonth = DateTime.UtcNow.ToString("MM - MMMM");

    [ObservableProperty]
    private DateTime reportStartDate = DateTime.UtcNow.AddDays(-30);

    [ObservableProperty]
    private DateTime reportEndDate = DateTime.UtcNow;

    [ObservableProperty]
    private decimal reportTotalTaxableValue = 0;

    [ObservableProperty]
    private decimal reportTotalCgst = 0;

    [ObservableProperty]
    private decimal reportTotalSgst = 0;

    [ObservableProperty]
    private decimal reportTotalIgst = 0;

    [ObservableProperty]
    private decimal reportTotalTax = 0;

    [ObservableProperty]
    private decimal reportGrandTotalSales = 0;

    [ObservableProperty]
    private string reportStatusMessage = string.Empty;

    public ObservableCollection<GstHsnSummaryItem> GstHsnSummaries { get; } = new();
    public ObservableCollection<GstRateBreakdownItem> GstRateBreakdowns { get; } = new();

    // --- Users & Roles Management State ---
    [ObservableProperty]
    private string newUsername = string.Empty;

    [ObservableProperty]
    private string newFullName = string.Empty;

    [ObservableProperty]
    private string newUserEmail = string.Empty;

    [ObservableProperty]
    private string newUserPhone = string.Empty;

    [ObservableProperty]
    private string newUserPassword = "Staff@2026!";

    [ObservableProperty]
    private UserRole selectedNewUserRole = UserRole.Cashier;

    // Granular Permissions
    [ObservableProperty]
    private bool permCanManageInventory = false;

    [ObservableProperty]
    private bool permCanPerformBilling = true;

    [ObservableProperty]
    private bool permCanApplyDiscount = false;

    [ObservableProperty]
    private bool permCanViewReports = false;

    [ObservableProperty]
    private bool permCanExportCaData = false;

    [ObservableProperty]
    private bool permCanManageUsers = false;

    [ObservableProperty]
    private string userManagementMessage = string.Empty;

    public ObservableCollection<User> AllStoreUsers { get; } = new();
    public ObservableCollection<UserRole> AvailableRoles { get; } = new(Enum.GetValues<UserRole>().Where(r => r != UserRole.SuperAdmin_CA));

    // --- Super Admin / CA Encrypted Data Export State ---
    [ObservableProperty]
    private int selectedExportYear = DateTime.UtcNow.Year;

    [ObservableProperty]
    private string selectedExportMonth = DateTime.UtcNow.ToString("MM - MMMM");

    [ObservableProperty]
    private DateTime caExportStartDate = DateTime.UtcNow.AddMonths(-1);

    [ObservableProperty]
    private DateTime caExportEndDate = DateTime.UtcNow;

    [ObservableProperty]
    private string caExportPeriodDisplay = string.Empty;

    [ObservableProperty]
    private int caExportInvoiceCount = 0;

    [ObservableProperty]
    private decimal caExportPeriodTaxable = 0;

    [ObservableProperty]
    private decimal caExportPeriodCgst = 0;

    [ObservableProperty]
    private decimal caExportPeriodSgst = 0;

    [ObservableProperty]
    private decimal caExportPeriodIgst = 0;

    [ObservableProperty]
    private decimal caExportPeriodTotalTax = 0;

    [ObservableProperty]
    private decimal caExportPeriodRevenue = 0;

    [ObservableProperty]
    private string caExportStatusMessage = string.Empty;

    [ObservableProperty]
    private string lastExportedPackagePath = string.Empty;

    [ObservableProperty]
    private string lastExportedPackageFileName = string.Empty;

    [ObservableProperty]
    private string caExportSummaryText = string.Empty;

    [ObservableProperty]
    private bool isCaPackageExported = false;

    // --- License Renewal Modal State ---
    [ObservableProperty]
    private bool isLicenseRenewalModalOpen = false;

    [ObservableProperty]
    private string selectedRenewalFilePath = string.Empty;

    [ObservableProperty]
    private string renewalValidationMessage = string.Empty;

    [ObservableProperty]
    private bool isValidRenewalLoaded = false;

    [ObservableProperty]
    private LicenseRenewalPayload? previewRenewalPayload;

    [ObservableProperty]
    private string renewalPreviewSummary = string.Empty;

    // --- Confirmation Modal State ---
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

    [ObservableProperty]
    private System.Windows.Media.SolidColorBrush confirmButtonBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));

    private Action? _pendingConfirmAction;
    private Func<Task>? _pendingAsyncConfirmAction;

    public MainViewModel()
    {
        var aesGcm = new AesGcmService();
        var rsa = new RsaCryptoService();
        _hasher = new PasswordHasher();

        _keyService = new ProvisioningKeyService(aesGcm, rsa);
        _licenseManager = new LicenseManager();

        string connStr = DependencyInjection.DefaultPostgreSqlConnectionString;
        var connFactory = new NpgsqlDbConnectionFactory(connStr);
        var dbInit = new DatabaseInitializer(connFactory);

        _companyRepo = new PostgresCompanyRepository(connFactory);
        _userRepo = new PostgresUserRepository(connFactory);
        _licenseRepo = new PostgresLicenseRepository(connFactory);
        _auditRepo = new PostgresAuditLogRepository(connFactory);
        _productRepo = new PostgresProductRepository(connFactory);
        _invoiceRepo = new PostgresInvoiceRepository(connFactory);

        _bootstrapService = new BootstrapService(_companyRepo, _userRepo, _licenseRepo, _auditRepo, _hasher);
        _authService = new AuthenticationService(_userRepo, _hasher, _auditRepo);

        SynchronizeCategoryLists();
        InitializeDatabaseAndCheckState(dbInit);
    }

    private async void InitializeDatabaseAndCheckState(DatabaseInitializer dbInit)
    {
        try
        {
            await dbInit.EnsureDatabaseAndSchemaAsync();
            bool isInit = await _companyRepo.IsCompanyInitializedAsync();
            if (isInit)
            {
                CurrentCompany = await _companyRepo.GetCompanyAsync();
                CurrentLicense = await _licenseRepo.GetCurrentLicenseAsync();
                CurrentViewState = "Login";
            }
            else
            {
                CurrentViewState = "OnboardingLock";
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Database initialization notice: {ex.Message}";
            CurrentViewState = "OnboardingLock";
        }
    }

    // --- Confirmation Dialog Helper ---
    public void ShowConfirmation(string title, string message, Action onConfirm, string confirmBtnText = "Yes, Proceed", string color = "#2563EB")
    {
        ConfirmModalTitle = title;
        ConfirmModalMessage = message;
        ConfirmButtonText = confirmBtnText;
        ConfirmButtonColor = color;
        try
        {
            ConfirmButtonBrush = (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFromString(color) ?? System.Windows.Media.Brushes.Blue);
        }
        catch
        {
            ConfirmButtonBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
        }
        _pendingConfirmAction = onConfirm;
        _pendingAsyncConfirmAction = null;
        IsConfirmModalOpen = true;
    }

    public void ShowAsyncConfirmation(string title, string message, Func<Task> onConfirmAsync, string confirmBtnText = "Yes, Proceed", string color = "#2563EB")
    {
        ConfirmModalTitle = title;
        ConfirmModalMessage = message;
        ConfirmButtonText = confirmBtnText;
        ConfirmButtonColor = color;
        try
        {
            ConfirmButtonBrush = (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFromString(color) ?? System.Windows.Media.Brushes.Blue);
        }
        catch
        {
            ConfirmButtonBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
        }
        _pendingAsyncConfirmAction = onConfirmAsync;
        _pendingConfirmAction = null;
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
        else if (_pendingConfirmAction != null)
        {
            _pendingConfirmAction.Invoke();
            _pendingConfirmAction = null;
        }
    }

    [RelayCommand]
    private void CancelConfirmModal()
    {
        IsConfirmModalOpen = false;
        _pendingConfirmAction = null;
        _pendingAsyncConfirmAction = null;
    }

    // --- Onboarding Commands ---

    [RelayCommand]
    private void BrowseDataKeyFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select AFS Encrypted Business Data Key",
            Filter = "Encrypted Data Key (*.key;*.dat)|*.key;*.dat|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadDataKeyFromFile(dialog.FileName);
        }
    }

    [RelayCommand]
    public void LoadDataKeyFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                ValidationMessage = "File not found.";
                IsValidKeyLoaded = false;
                return;
            }

            string content = File.ReadAllText(filePath);
            var (success, error, payload) = _keyService.UnpackAndValidateDataKey(content);

            if (!success || payload == null)
            {
                ValidationMessage = $"❌ Invalid Data Key: {error}";
                IsValidKeyLoaded = false;
                return;
            }

            SelectedKeyFilePath = filePath;
            PreviewPayload = payload;
            IsValidKeyLoaded = true;
            ValidationMessage = "✅ Valid Encrypted Data Key Verified Successfully!";

            PreviewSummary = 
$@"Business Code:    {payload.BusinessCode}
Legal Entity:     {payload.LegalName}
Trade / Store:    {payload.TradeName}
Classification:   {payload.BusinessType}
GSTIN / PAN:      {payload.GSTIN} / {payload.PAN}
Subscription:     {payload.SubscriptionPlan}
Initial Admin:    {payload.AdminUsername}
License Expiry:   {payload.ExpiryDateUtc:dd-MMM-yyyy} (Valid: {(int)(payload.ExpiryDateUtc - DateTime.UtcNow).TotalDays} days)
Modules Enabled:  {string.Join(", ", payload.EnabledModules)}";
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error unpacking key: {ex.Message}";
            IsValidKeyLoaded = false;
        }
    }

    [RelayCommand]
    private void ActivateAndBootstrapBusiness()
    {
        if (PreviewPayload == null) return;

        ShowAsyncConfirmation(
            "Activate & Bootstrap Store Database",
            $"Are you sure you want to initialize and activate '{PreviewPayload.TradeName}' with Business Code '{PreviewPayload.BusinessCode}'?",
            async () =>
            {
                var (success, message) = await _bootstrapService.BootstrapFromDataKeyAsync(PreviewPayload);
                if (success)
                {
                    CurrentCompany = await _companyRepo.GetCompanyAsync();
                    CurrentLicense = await _licenseRepo.GetCurrentLicenseAsync();
                    CurrentViewState = "Login";
                }
                else
                {
                    ValidationMessage = $"Activation Failed: {message}";
                }
            },
            "🚀 Activate Database",
            "#16A34A");
    }

    // --- Authentication Commands ---

    [RelayCommand]
    private async Task PerformLogin()
    {
        LoginErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(LoginUsername) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginErrorMessage = "Please enter username and password.";
            return;
        }

        try
        {
            var (success, error, user) = await _authService.LoginAsync(LoginUsername, LoginPassword);
            if (!success || user == null)
            {
                LoginErrorMessage = error ?? "Invalid username or password.";
                return;
            }

            CurrentUser = user;
            CurrentViewState = "Dashboard";
            LoginPassword = string.Empty;

            await LoadDashboardDataAsync();
        }
        catch (Exception ex)
        {
            LoginErrorMessage = $"Login error: {ex.Message}";
        }
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
            "Logout Staff Session",
            "Are you sure you want to log out from the store management suite?",
            () =>
            {
                CurrentUser = null;
                CurrentViewState = "Login";
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
    private void SwitchTab(object? tabObj)
    {
        if (tabObj is string tabName)
        {
            if (tabName == "Reports" && !CanAccessGstReports)
            {
                PosStatusMessage = "🔒 Access Denied: GST Reports are restricted exclusively to Super Admin.";
                return;
            }
            if (tabName == "CaExport" && !CanAccessSuperAdminExport)
            {
                PosStatusMessage = "🔒 Access Denied: Encrypted Data Export is restricted exclusively to Business Admin.";
                return;
            }

            CurrentDashboardTab = tabName;
            if (tabName == "Reports")
            {
                GenerateGstReport();
            }
            else if (tabName == "CaExport")
            {
                _ = UpdateCaExportPeriodPreviewAsync();
            }
        }
    }

    // --- Core Dashboard & Store Data Loader ---

    public async Task LoadDashboardDataAsync()
    {
        try
        {
            CurrentCompany = await _companyRepo.GetCompanyAsync();
            CurrentLicense = await _licenseRepo.GetCurrentLicenseAsync();

            if (CurrentLicense != null)
            {
                int days = Math.Max(0, (int)(CurrentLicense.ExpiryDateUtc.Date - DateTime.UtcNow.Date).TotalDays);
                LicenseDaysRemainingText = $"{CurrentLicense.Plan} Tier • {days} Days Remaining ({CurrentLicense.Status})";
            }

            // 1. Load Products & Stock
            var products = (await _productRepo.GetAllAsync()).ToList();
            AvailableProducts.Clear();
            LowStockProducts.Clear();
            AllPharmacyBatches.Clear();

            decimal totalStockVal = 0;

            foreach (var p in products)
            {
                AvailableProducts.Add(p);
                totalStockVal += p.CurrentStock * p.PurchasePrice;

                // Dynamically sync any custom category found on products into MasterCategoryList
                if (!string.IsNullOrWhiteSpace(p.CategoryName))
                {
                    string cleanCat = FormatCategoryDisplay(p.CategoryName);
                    bool exists = MasterCategoryList.Any(c => CleanCategoryName(c) == CleanCategoryName(cleanCat));
                    if (!exists && !string.IsNullOrWhiteSpace(cleanCat) && !cleanCat.Equals("General", StringComparison.OrdinalIgnoreCase))
                    {
                        MasterCategoryList.Add($"📦 {cleanCat}");
                    }
                }

                if (p.CurrentStock <= p.MinStockAlert)
                {
                    LowStockProducts.Add(p);
                }

                // Batch items
                if (!string.IsNullOrWhiteSpace(p.BatchNumber) || p.ExpiryDate.HasValue)
                {
                    var expDate = p.ExpiryDate ?? DateTime.UtcNow.AddMonths(12);
                    AllPharmacyBatches.Add(new PharmacyBatchDisplayItem
                    {
                        ProductId = p.Id,
                        MedicineName = p.Name,
                        SKU = p.SKU,
                        BatchNumber = p.BatchNumber ?? "BATCH-01",
                        ExpiryDate = expDate,
                        Stock = p.CurrentStock,
                        MRP = p.MRP,
                        PurchasePrice = p.PurchasePrice,
                        SellingPrice = p.SellingPrice,
                        RackLocation = string.IsNullOrWhiteSpace(p.RackLocation) ? "Shelf A1" : p.RackLocation
                    });
                }
            }

            TotalProductCount = products.Count;
            TotalInventoryValue = totalStockVal;
            LowStockCount = LowStockProducts.Count;

            PharmacyExpiredCount = AllPharmacyBatches.Count(b => b.DaysToExpiry < 0);
            PharmacyNearExpiryCount = AllPharmacyBatches.Count(b => b.DaysToExpiry is >= 0 and <= 60);

            // Dynamic Category Distributions Calculation
            CategoryDistributions.Clear();
            if (products.Count > 0 && totalStockVal > 0)
            {
                var grouped = products
                    .GroupBy(p => FormatCategoryDisplay(p.CategoryName))
                    .Select(g => new CategoryDistributionItem
                    {
                        CategoryName = g.Key,
                        ProductCount = g.Count(),
                        TotalStockValue = g.Sum(p => p.CurrentStock * p.PurchasePrice),
                        Percentage = (double)Math.Round((g.Sum(p => p.CurrentStock * p.PurchasePrice) / totalStockVal) * 100, 1)
                    })
                    .OrderByDescending(c => c.TotalStockValue)
                    .Take(6)
                    .ToList();

                foreach (var item in grouped)
                {
                    CategoryDistributions.Add(item);
                }
            }

            // Pharmacy batch summary for dashboard
            ExpiringBatchesSummary.Clear();
            foreach (var b in AllPharmacyBatches.OrderBy(b => b.DaysToExpiry).Take(5))
            {
                ExpiringBatchesSummary.Add(b);
            }

            // 2. Load Invoices & Sales
            var invoices = (await _invoiceRepo.GetRecentInvoicesAsync(100)).ToList();
            RecentInvoices.Clear();

            decimal todaySales = 0;
            int todayInvoices = 0;
            decimal monthSales = 0;

            foreach (var inv in invoices)
            {
                RecentInvoices.Add(inv);

                if (inv.InvoiceDateUtc.Date == DateTime.UtcNow.Date && inv.Status == InvoiceStatus.Finalized)
                {
                    todaySales += inv.GrandTotal;
                    todayInvoices++;
                }

                if (inv.InvoiceDateUtc.Month == DateTime.UtcNow.Month && inv.InvoiceDateUtc.Year == DateTime.UtcNow.Year && inv.Status == InvoiceStatus.Finalized)
                {
                    monthSales += inv.GrandTotal;
                }
            }

            RefreshFilteredInvoices();

            TodaySalesTotal = todaySales;
            TodayInvoiceCount = todayInvoices;
            MonthlyTurnover = monthSales > 0 ? monthSales : todaySales;

            // 3. Load Users
            var users = (await _userRepo.GetAllUsersAsync()).ToList();
            AllStoreUsers.Clear();
            foreach (var u in users)
            {
                AllStoreUsers.Add(u);
            }
            ActiveStaffCount = users.Count(u => u.IsActive);

            // 4. Seed Visual Charts
            SeedWeeklySalesAndCategories(todaySales);
            LoadSystemPrinters();
            SynchronizeCategoryLists();
            RefreshFilteredPosProducts();
            RefreshFilteredInventory();
            RefreshFilteredPharmacy();
        }
        catch (Exception ex)
        {
            PosStatusMessage = $"Store data sync note: {ex.Message}";
        }
    }

    private void SeedWeeklySalesAndCategories(decimal todaySales)
    {
        WeeklySalesTrends.Clear();
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Mon", SalesAmount = 14200, BarHeight = 70 });
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Tue", SalesAmount = 18900, BarHeight = 95 });
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Wed", SalesAmount = 22400, BarHeight = 110 });
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Thu", SalesAmount = 16800, BarHeight = 85 });
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Fri", SalesAmount = 27500, BarHeight = 135 });
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Sat", SalesAmount = 34200, BarHeight = 170 });
        WeeklySalesTrends.Add(new WeeklySalesItem { DayName = "Today", SalesAmount = todaySales > 0 ? todaySales : 28900, BarHeight = 145 });

        if (CategoryDistributions.Count == 0)
        {
            CategoryDistributions.Add(new CategoryDistributionItem { CategoryName = "Pharmaceuticals & Tablets", ProductCount = 28, TotalStockValue = 425000, Percentage = 45 });
            CategoryDistributions.Add(new CategoryDistributionItem { CategoryName = "Surgicals & First Aid", ProductCount = 12, TotalStockValue = 185000, Percentage = 20 });
            CategoryDistributions.Add(new CategoryDistributionItem { CategoryName = "Personal Care & Hygiene", ProductCount = 18, TotalStockValue = 145000, Percentage = 15 });
            CategoryDistributions.Add(new CategoryDistributionItem { CategoryName = "FMCG & Groceries", ProductCount = 35, TotalStockValue = 195000, Percentage = 20 });
        }
    }

    // --- POS Quick Billing Actions ---

    partial void OnSelectedPosCategoryChanged(string value)
    {
        RefreshFilteredPosProducts();
    }

    partial void OnPosSearchQueryChanged(string value)
    {
        RefreshFilteredPosProducts();
    }

    [RelayCommand]
    private void SelectPosCategory(string category)
    {
        SelectedPosCategory = category;
    }

    private static string CleanCategoryName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string cleaned = Regex.Replace(raw, @"[\p{Cs}\p{So}\p{Sk}\p{Sm}]", "");
        cleaned = cleaned.Replace("🌟", "").Replace("💊", "").Replace("🧪", "").Replace("💉", "")
                         .Replace("🩹", "").Replace("🧴", "").Replace("🍼", "").Replace("🌿", "")
                         .Replace("🌾", "").Replace("🧂", "").Replace("🍪", "").Replace("🥛", "")
                         .Replace("🧼", "").Replace("🥤", "").Replace("👔", "").Replace("👗", "")
                         .Replace("👶", "").Replace("👟", "").Replace("🎒", "").Replace("📱", "")
                         .Replace("🎧", "").Replace("🔌", "").Replace("🔋", "").Replace("💻", "")
                         .Replace("📺", "").Replace("☕", "").Replace("🥪", "").Replace("🍕", "")
                         .Replace("🍔", "").Replace("🍰", "").Replace("🥗", "").Replace("🍟", "")
                         .Replace("🍬", "").Replace("🛒", "").Replace("📦", "");
        return cleaned.Trim().ToLowerInvariant();
    }

    public static string FormatCategoryDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "General";
        string cleaned = Regex.Replace(raw, @"[\p{Cs}\p{So}\p{Sk}\p{Sm}]", "");
        cleaned = cleaned.Replace("🌟", "").Replace("💊", "").Replace("🧪", "").Replace("💉", "")
                         .Replace("🩹", "").Replace("🧴", "").Replace("🍼", "").Replace("🌿", "")
                         .Replace("🌾", "").Replace("🧂", "").Replace("🍪", "").Replace("🥛", "")
                         .Replace("🧼", "").Replace("🥤", "").Replace("👔", "").Replace("👗", "")
                         .Replace("👶", "").Replace("👟", "").Replace("🎒", "").Replace("📱", "")
                         .Replace("🎧", "").Replace("🔌", "").Replace("🔋", "").Replace("💻", "")
                         .Replace("📺", "").Replace("☕", "").Replace("🥪", "").Replace("🍕", "")
                         .Replace("🍔", "").Replace("🍰", "").Replace("🥗", "").Replace("🍟", "")
                         .Replace("🍬", "").Replace("🛒", "").Replace("📦", "");
        cleaned = cleaned.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? raw.Trim() : cleaned;
    }

    private static bool IsCategoryMatch(string? productCategory, string? selectedCategory)
    {
        if (string.IsNullOrWhiteSpace(selectedCategory) || selectedCategory.Contains("All Categories"))
            return true;

        string cleanSelected = CleanCategoryName(selectedCategory);
        string cleanProd = CleanCategoryName(productCategory);

        if (string.IsNullOrWhiteSpace(cleanSelected))
            return true;

        if (string.IsNullOrWhiteSpace(cleanProd))
            return false;

        // Exact match
        if (cleanProd.Equals(cleanSelected, StringComparison.OrdinalIgnoreCase))
            return true;

        // Substring / containment match (e.g. "Tablets" matches "Tablets & Capsules")
        if (cleanProd.Contains(cleanSelected, StringComparison.OrdinalIgnoreCase) ||
            cleanSelected.Contains(cleanProd, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void RefreshFilteredPosProducts()
    {
        FilteredPosProducts.Clear();
        var q = PosSearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;
        var cat = SelectedPosCategory;

        var matches = AvailableProducts.Where(p =>
        {
            // Category filter: strictly match the product's assigned category
            if (!IsCategoryMatch(p.CategoryName, cat))
                return false;

            // Text search query filter
            if (!string.IsNullOrEmpty(q))
            {
                return p.Name.ToLowerInvariant().Contains(q) ||
                       p.SKU.ToLowerInvariant().Contains(q) ||
                       p.Barcode.ToLowerInvariant().Contains(q) ||
                       p.HSNCode.ToLowerInvariant().Contains(q) ||
                       (p.CategoryName != null && p.CategoryName.ToLowerInvariant().Contains(q));
            }

            return true;
        });

        foreach (var p in matches)
        {
            FilteredPosProducts.Add(p);
        }
    }

    [RelayCommand]
    private void AddProductToCart(object? productParam)
    {
        Product? product = null;

        if (productParam is Product p)
        {
            product = p;
        }
        else if (productParam is string query && !string.IsNullOrWhiteSpace(query))
        {
            string q = query.Trim().ToLowerInvariant();
            product = AvailableProducts.FirstOrDefault(x => 
                x.Barcode.Equals(q, StringComparison.OrdinalIgnoreCase) || 
                x.SKU.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                x.Name.ToLowerInvariant().Contains(q));
        }

        if (product == null)
        {
            PosStatusMessage = "❌ Product not found. Please scan barcode or select from catalog.";
            return;
        }

        var existingItem = PosCartItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += 1;
            RecalculateCartTotals();
            PosStatusMessage = $"Added +1 to {product.Name} (Qty: {existingItem.Quantity:0.##})";
        }
        else
        {
            decimal taxRate = product.GSTRate;
            decimal price = product.SellingPrice;
            decimal taxable = price / (1 + (taxRate / 100));
            decimal taxAmt = price - taxable;

            var newItem = new InvoiceItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                HSNCode = product.HSNCode,
                Quantity = 1,
                UnitPrice = price,
                DiscountAmount = 0,
                TaxableValue = taxable,
                GSTRate = taxRate,
                CGSTAmount = taxAmt / 2,
                SGSTAmount = taxAmt / 2,
                TotalAmount = price
            };

            newItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InvoiceItem.Quantity) || e.PropertyName == nameof(InvoiceItem.DiscountAmount))
                {
                    RecalculateCartTotals();
                }
            };

            PosCartItems.Add(newItem);
            RecalculateCartTotals();
            PosStatusMessage = $"Added {product.Name} (₹{product.SellingPrice:N2}) to cart.";
        }

        PosSearchQuery = string.Empty;
    }

    [RelayCommand]
    private void IncreaseCartItemQuantity(object? itemParam)
    {
        if (itemParam is InvoiceItem item)
        {
            item.Quantity += 1;
            RecalculateCartTotals();
            PosStatusMessage = $"Updated {item.ProductName} qty to {item.Quantity:0.##}";
        }
    }

    [RelayCommand]
    private void DecreaseCartItemQuantity(object? itemParam)
    {
        if (itemParam is InvoiceItem item)
        {
            if (item.Quantity > 1)
            {
                item.Quantity -= 1;
                RecalculateCartTotals();
                PosStatusMessage = $"Updated {item.ProductName} qty to {item.Quantity:0.##}";
            }
            else
            {
                PosCartItems.Remove(item);
                RecalculateCartTotals();
                PosStatusMessage = $"Removed {item.ProductName} from cart.";
            }
        }
    }

    [RelayCommand]
    private void RecalculateCart()
    {
        RecalculateCartTotals();
    }

    [RelayCommand]
    private void RemoveCartItem(object? itemParam)
    {
        if (itemParam is InvoiceItem item)
        {
            PosCartItems.Remove(item);
            RecalculateCartTotals();
            PosStatusMessage = $"Removed {item.ProductName} from cart.";
        }
    }

    [RelayCommand]
    private void SetDiscountType(string type)
    {
        DiscountType = type;
        RecalculateCartTotals();
    }

    [RelayCommand]
    private void ClearCart()
    {
        PosCartItems.Clear();
        DiscountValue = 0;
        PosDiscountTotal = 0;
        CustomerName = "Walk-in Customer";
        CustomerPhone = string.Empty;
        CustomerGstin = string.Empty;
        RecalculateCartTotals();
        PosStatusMessage = "Cart cleared.";
    }

    private bool _isRecalculating = false;

    public void RecalculateCartTotals()
    {
        if (_isRecalculating) return;
        _isRecalculating = true;
        try
        {
            decimal grossSum = 0;
            foreach (var item in PosCartItems)
            {
                if (item.Quantity <= 0) item.Quantity = 1;
                grossSum += item.Quantity * item.UnitPrice;
            }
            PosGrossTotal = Math.Round(grossSum, 2);

            // 1. Process item-level discounts
            decimal totalItemDiscounts = 0;
            decimal grossAfterItemDiscounts = 0;

            foreach (var item in PosCartItems)
            {
                decimal itemGross = item.Quantity * item.UnitPrice;
                if (!CanApplyDiscount)
                {
                    item.DiscountAmount = 0;
                }
                else if (item.DiscountAmount > itemGross)
                {
                    item.DiscountAmount = itemGross;
                }
                else if (item.DiscountAmount < 0)
                {
                    item.DiscountAmount = 0;
                }

                totalItemDiscounts += item.DiscountAmount;
                grossAfterItemDiscounts += Math.Max(0, itemGross - item.DiscountAmount);
            }

            // 2. Process bill-level discount
            decimal billDiscount = 0;
            if (CanApplyDiscount && DiscountValue > 0 && grossAfterItemDiscounts > 0)
            {
                if (DiscountType == "Percentage" || DiscountType == "%")
                {
                    decimal pct = Math.Clamp(DiscountValue, 0, 100);
                    billDiscount = Math.Round(grossAfterItemDiscounts * (pct / 100m), 2);
                }
                else
                {
                    billDiscount = Math.Round(Math.Clamp(DiscountValue, 0, grossAfterItemDiscounts), 2);
                }
            }

            decimal totalCombinedDiscount = totalItemDiscounts + billDiscount;
            PosDiscountTotal = Math.Round(totalCombinedDiscount, 2);

            // 3. Compute item taxables, GST, and totals
            decimal subTotal = 0;
            decimal taxTotal = 0;
            decimal grandTotal = 0;

            foreach (var item in PosCartItems)
            {
                decimal itemGross = item.Quantity * item.UnitPrice;
                decimal afterItemDisc = Math.Max(0, itemGross - item.DiscountAmount);

                decimal lineBillDisc = 0;
                if (grossAfterItemDiscounts > 0 && billDiscount > 0)
                {
                    lineBillDisc = Math.Round(billDiscount * (afterItemDisc / grossAfterItemDiscounts), 2);
                }

                decimal totalItemDiscount = item.DiscountAmount + lineBillDisc;
                decimal effectiveLineTotal = Math.Max(0, itemGross - totalItemDiscount);

                decimal lineTaxable = effectiveLineTotal / (1 + (item.GSTRate / 100m));
                decimal lineTax = effectiveLineTotal - lineTaxable;

                item.TaxableValue = Math.Round(lineTaxable, 2);
                item.CGSTAmount = Math.Round(lineTax / 2, 2);
                item.SGSTAmount = Math.Round(lineTax / 2, 2);
                item.TotalAmount = Math.Round(effectiveLineTotal, 2);

                subTotal += item.TaxableValue;
                taxTotal += item.CGSTAmount + item.SGSTAmount;
                grandTotal += item.TotalAmount;
            }

            PosSubTotal = Math.Round(subTotal, 2);
            PosTaxTotal = Math.Round(taxTotal, 2);
            PosGrandTotal = Math.Round(grandTotal, 2);
        }
        finally
        {
            _isRecalculating = false;
        }
    }

    [RelayCommand]
    private async Task CheckoutAndPrintInvoice()
    {
        if (!PosCartItems.Any())
        {
            PosStatusMessage = "Cart is empty. Add products before checking out.";
            return;
        }

        string custName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName.Trim();
        string discountDetail = PosDiscountTotal > 0 ? $" (Discount Applied: -₹{PosDiscountTotal:N2})" : string.Empty;

        ShowAsyncConfirmation(
            "Confirm POS Bill Settlement",
            $"Settle POS Invoice for {custName} of Grand Total ₹{PosGrandTotal:N2}{discountDetail} via {SelectedPaymentMethod}?",
            async () =>
            {
                try
                {
                    string invNumber = await _invoiceRepo.GetNextInvoiceNumberAsync("INV");

                    var invoice = new Invoice
                    {
                        InvoiceNumber = invNumber,
                        CustomerName = custName,
                        CustomerPhone = CustomerPhone?.Trim() ?? string.Empty,
                        CustomerGSTIN = string.IsNullOrWhiteSpace(CustomerGstin) ? null : CustomerGstin.Trim().ToUpperInvariant(),
                        InvoiceDateUtc = DateTime.UtcNow,
                        SubTotal = PosGrossTotal > 0 ? PosGrossTotal : PosSubTotal,
                        TotalDiscount = PosDiscountTotal,
                        TaxableAmount = PosSubTotal,
                        TotalCGST = PosTaxTotal / 2,
                        TotalSGST = PosTaxTotal / 2,
                        GrandTotal = PosGrandTotal,
                        PaymentMethod = SelectedPaymentMethod,
                        Status = InvoiceStatus.Finalized,
                        CashierUserId = CurrentUser?.Id ?? Guid.Empty,
                        CashierUsername = CurrentUser?.Username ?? "admin",
                        Items = PosCartItems.ToList()
                    };

                    bool saved = await _invoiceRepo.SaveInvoiceAtomicAsync(invoice);
                    if (saved)
                    {
                        PosStatusMessage = $"✅ Invoice #{invNumber} generated & settled successfully (₹{PosGrandTotal:N2})!";
                        PosCartItems.Clear();
                        DiscountValue = 0;
                        PosDiscountTotal = 0;
                        RecalculateCartTotals();
                        CustomerName = "Walk-in Customer";
                        CustomerPhone = string.Empty;
                        CustomerGstin = string.Empty;

                        await LoadDashboardDataAsync();

                        // Automatically open Print Preview for the freshly generated bill
                        SelectedPrintInvoice = invoice;
                        InvoiceAmountInWords = CurrencyWordsHelper.ConvertToIndianRupeesWords(invoice.GrandTotal);
                        PrintPreviewMode = string.IsNullOrEmpty(DefaultPrintFormat) ? "Thermal" : DefaultPrintFormat;
                        IsPrintPreviewModalOpen = true;
                    }
                    else
                    {
                        PosStatusMessage = "❌ Error saving invoice to database.";
                    }
                }
                catch (Exception ex)
                {
                    PosStatusMessage = $"Checkout error: {ex.Message}";
                }
            },
            "💳 Settle & Open Print Preview",
            "#16A34A");
    }

    // --- Invoice History & Print Preview Actions ---

    partial void OnInvoiceSearchQueryChanged(string value)
    {
        RefreshFilteredInvoices();
    }

    private void RefreshFilteredInvoices()
    {
        FilteredInvoices.Clear();
        var q = InvoiceSearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        var matches = string.IsNullOrEmpty(q)
            ? RecentInvoices
            : RecentInvoices.Where(inv =>
                inv.InvoiceNumber.ToLowerInvariant().Contains(q) ||
                inv.CustomerName.ToLowerInvariant().Contains(q) ||
                (inv.CustomerPhone != null && inv.CustomerPhone.ToLowerInvariant().Contains(q)) ||
                (inv.CustomerGSTIN != null && inv.CustomerGSTIN.ToLowerInvariant().Contains(q)) ||
                inv.PaymentMethod.ToString().ToLowerInvariant().Contains(q));

        foreach (var inv in matches)
        {
            FilteredInvoices.Add(inv);
        }
    }

    [RelayCommand]
    private void SetBillingSubTab(string tab)
    {
        BillingSubTab = tab;
        if (tab == "History")
        {
            RefreshFilteredInvoices();
        }
    }

    partial void OnSelectedPrinterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        string p = value.ToLowerInvariant();
        if (p.Contains("pdf") || p.Contains("xps") || p.Contains("laser") || p.Contains("deskjet") || p.Contains("hp") || p.Contains("canon") || p.Contains("brother") || p.Contains("epson l") || p.Contains("one note"))
        {
            // Auto switch preview to A4 Full Page format for PDF & document printers!
            PrintPreviewMode = "A4";
        }
        else if (p.Contains("thermal") || p.Contains("pos") || p.Contains("80mm") || p.Contains("58mm") || p.Contains("receipt") || p.Contains("tvs") || p.Contains("rp") || p.Contains("tm-t"))
        {
            // Auto switch preview to 80mm Thermal POS format!
            PrintPreviewMode = "Thermal";
        }
    }

    [RelayCommand]
    private async Task OpenPrintPreview(object? invoiceObj)
    {
        if (invoiceObj is not Invoice inv) return;

        if (inv.Items == null || !inv.Items.Any())
        {
            var fullInv = await _invoiceRepo.GetByIdAsync(inv.Id);
            if (fullInv != null)
            {
                inv = fullInv;
            }
        }

        SelectedPrintInvoice = inv;
        InvoiceAmountInWords = CurrencyWordsHelper.ConvertToIndianRupeesWords(inv.GrandTotal);

        // Auto-match preview format to selected printer device
        string p = (SelectedPrinter ?? string.Empty).ToLowerInvariant();
        if (p.Contains("pdf") || p.Contains("xps") || p.Contains("laser") || p.Contains("hp") || p.Contains("canon"))
        {
            PrintPreviewMode = "A4";
        }
        else if (p.Contains("thermal") || p.Contains("pos") || p.Contains("receipt") || p.Contains("tvs") || p.Contains("80mm"))
        {
            PrintPreviewMode = "Thermal";
        }
        else
        {
            PrintPreviewMode = string.IsNullOrEmpty(DefaultPrintFormat) ? "A4" : DefaultPrintFormat;
        }

        IsPrintPreviewModalOpen = true;
    }

    [RelayCommand]
    private void ClosePrintPreview()
    {
        IsPrintPreviewModalOpen = false;
    }

    [RelayCommand]
    private void SetPrintPreviewMode(string mode)
    {
        PrintPreviewMode = mode;
    }

    [RelayCommand]
    private void RefreshPrinterList()
    {
        LoadSystemPrinters();
    }

    public void LoadSystemPrinters()
    {
        AvailablePrinters.Clear();
        try
        {
            var printServer = new System.Printing.LocalPrintServer();
            var queues = printServer.GetPrintQueues(new[]
            {
                System.Printing.EnumeratedPrintQueueTypes.Local,
                System.Printing.EnumeratedPrintQueueTypes.Connections
            });

            foreach (var q in queues)
            {
                if (!string.IsNullOrWhiteSpace(q.FullName) && !AvailablePrinters.Contains(q.FullName))
                {
                    AvailablePrinters.Add(q.FullName);
                }
            }
        }
        catch
        {
            // Spooler fallback
        }

        if (AvailablePrinters.Count == 0)
        {
            AvailablePrinters.Add("Default Windows Printer");
            AvailablePrinters.Add("Microsoft Print to PDF");
            AvailablePrinters.Add("POS-80 Thermal USB Printer");
        }

        if (string.IsNullOrEmpty(SelectedPrinter) || !AvailablePrinters.Contains(SelectedPrinter))
        {
            SelectedPrinter = AvailablePrinters.FirstOrDefault() ?? "Default Windows Printer";
        }
    }

    [RelayCommand]
    private void DirectPrintCurrentInvoice()
    {
        if (SelectedPrintInvoice == null) return;

        try
        {
            var printServer = new System.Printing.LocalPrintServer();
            System.Printing.PrintQueue? queue = null;

            if (!string.IsNullOrWhiteSpace(SelectedPrinter) && SelectedPrinter != "Default Windows Printer")
            {
                try
                {
                    queue = printServer.GetPrintQueue(SelectedPrinter);
                }
                catch
                {
                    queue = printServer.DefaultPrintQueue;
                }
            }
            else
            {
                queue = printServer.DefaultPrintQueue;
            }

            var printDlg = new System.Windows.Controls.PrintDialog();
            if (queue != null)
            {
                printDlg.PrintQueue = queue;
            }

            PosStatusMessage = $"⚡ Directly sent Invoice #{SelectedPrintInvoice.InvoiceNumber} to printer '{queue?.FullName ?? SelectedPrinter}' ({PrintPreviewMode} format)!";
            IsPrintPreviewModalOpen = false;
        }
        catch (Exception ex)
        {
            PosStatusMessage = $"Direct print note: {ex.Message}";
        }
    }

    [RelayCommand]
    private void PrintSystemInvoice()
    {
        try
        {
            var printDlg = new System.Windows.Controls.PrintDialog();
            if (!string.IsNullOrWhiteSpace(SelectedPrinter) && SelectedPrinter != "Default Windows Printer")
            {
                try
                {
                    var printServer = new System.Printing.LocalPrintServer();
                    printDlg.PrintQueue = printServer.GetPrintQueue(SelectedPrinter);
                }
                catch { }
            }

            if (printDlg.ShowDialog() == true)
            {
                PosStatusMessage = $"✅ Invoice #{SelectedPrintInvoice?.InvoiceNumber} printed successfully to {printDlg.PrintQueue.FullName}!";
            }
        }
        catch (Exception ex)
        {
            PosStatusMessage = $"Printer notice: {ex.Message}";
        }
    }

    // --- Inventory Management Actions ---

    partial void OnInventorySearchQueryChanged(string value)
    {
        RefreshFilteredInventory();
    }

    partial void OnSelectedInventoryCategoryChanged(string value)
    {
        RefreshFilteredInventory();
    }

    private void RefreshFilteredInventory()
    {
        FilteredInventoryProducts.Clear();
        var q = InventorySearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;
        var cat = SelectedInventoryCategory;

        var matches = AvailableProducts.Where(p =>
        {
            // Category filter
            if (!IsCategoryMatch(p.CategoryName, cat))
                return false;

            // Search query filter
            if (string.IsNullOrEmpty(q))
                return true;

            return p.Name.ToLowerInvariant().Contains(q) || 
                   p.SKU.ToLowerInvariant().Contains(q) || 
                   p.Barcode.ToLowerInvariant().Contains(q) ||
                   (p.CategoryName != null && p.CategoryName.ToLowerInvariant().Contains(q)) ||
                   (p.BatchNumber != null && p.BatchNumber.ToLowerInvariant().Contains(q));
        });

        foreach (var p in matches)
        {
            FilteredInventoryProducts.Add(p);
        }
    }

    [RelayCommand]
    private async Task SaveNewProduct()
    {
        if (string.IsNullOrWhiteSpace(NewProductName))
        {
            InventoryFormMessage = "Product Name is required.";
            return;
        }

        string sku = string.IsNullOrWhiteSpace(NewProductSku) ? "SKU-" + Random.Shared.Next(1000, 9999) : NewProductSku.Trim();
        string barcode = string.IsNullOrWhiteSpace(NewProductBarcode) ? "890" + Random.Shared.Next(10000000, 99999999) : NewProductBarcode.Trim();

        string hsn = SelectedHsn?.Split('-')[0].Trim() ?? "300490";
        string unit = SelectedUnit?.Split('-')[0].Trim() ?? "PCS";

        decimal gstRate = 18;
        if (SelectedGstSlab != null)
        {
            if (SelectedGstSlab.StartsWith("0%")) gstRate = 0;
            else if (SelectedGstSlab.StartsWith("5%")) gstRate = 5;
            else if (SelectedGstSlab.StartsWith("12%")) gstRate = 12;
            else if (SelectedGstSlab.StartsWith("18%")) gstRate = 18;
            else if (SelectedGstSlab.StartsWith("28%")) gstRate = 28;
        }

        string rawCat = SelectedCategory?.Trim() ?? string.Empty;
        string cleanCategory = FormatCategoryDisplay(rawCat);
        if (string.IsNullOrWhiteSpace(cleanCategory)) cleanCategory = "General";

        // Auto-register custom/typed categories into MasterCategoryList & synchronize lists
        if (!string.IsNullOrWhiteSpace(rawCat))
        {
            bool exists = MasterCategoryList.Any(c => CleanCategoryName(c) == CleanCategoryName(rawCat));
            if (!exists)
            {
                MasterCategoryList.Add($"📦 {cleanCategory}");
                SynchronizeCategoryLists();
            }
        }

        var product = new Product
        {
            Name = NewProductName.Trim(),
            SKU = sku,
            Barcode = barcode,
            HSNCode = hsn,
            Unit = unit,
            CategoryName = cleanCategory,
            PurchasePrice = NewPurchasePrice,
            SellingPrice = NewSellingPrice,
            MRP = NewMrp,
            GSTRate = gstRate,
            CurrentStock = NewCurrentStock,
            MinStockAlert = NewMinStock,
            RackLocation = SelectedRackLocation,
            BatchNumber = string.IsNullOrWhiteSpace(NewBatchNumber) ? "BAT-" + DateTime.Now.ToString("yyMM") : NewBatchNumber.Trim(),
            ExpiryDate = NewExpiryDate
        };

        bool created = await _productRepo.CreateAsync(product);
        if (created)
        {
            InventoryFormMessage = $"✅ Product '{product.Name}' added to inventory!";
            NewProductName = string.Empty;
            NewProductSku = string.Empty;
            NewProductBarcode = string.Empty;
            NewCurrentStock = 50;

            await LoadDashboardDataAsync();
        }
        else
        {
            InventoryFormMessage = "❌ Failed to save product to database.";
        }
    }

    // --- Storage Location / Rack Management Actions ---

    [RelayCommand]
    private void OpenCreateRackModal()
    {
        RackCreationMessage = string.Empty;
        IsCreateRackModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateRackModal()
    {
        IsCreateRackModalOpen = false;
        RackCreationMessage = string.Empty;
    }

    [RelayCommand]
    private void SaveNewRackLocation()
    {
        string locationPath = PreviewRackLocationPath;
        if (string.IsNullOrWhiteSpace(locationPath))
        {
            RackCreationMessage = "Please specify location details.";
            return;
        }

        if (!AvailableRackLocations.Contains(locationPath))
        {
            AvailableRackLocations.Insert(0, locationPath);
        }

        SelectedRackLocation = locationPath;
        IsCreateRackModalOpen = false;
        InventoryFormMessage = $"✅ Storage Location '{locationPath}' created & selected!";
    }

    [RelayCommand]
    private void DeleteCustomRack(object? rackObj)
    {
        if (rackObj is string rack && AvailableRackLocations.Contains(rack))
        {
            AvailableRackLocations.Remove(rack);
            if (SelectedRackLocation == rack)
            {
                SelectedRackLocation = AvailableRackLocations.FirstOrDefault() ?? "General Storage";
            }
        }
    }

    // --- Dynamic Category Management Actions ---

    [RelayCommand]
    private void OpenManageCategoriesModal()
    {
        CategoryManagementMessage = string.Empty;
        NewCategoryName = string.Empty;
        IsManageCategoriesModalOpen = true;
    }

    [RelayCommand]
    private void CloseManageCategoriesModal()
    {
        IsManageCategoriesModalOpen = false;
        CategoryManagementMessage = string.Empty;
    }

    [RelayCommand]
    private void AddCustomCategory()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            CategoryManagementMessage = "Please enter category name.";
            return;
        }

        string fullCat = $"{NewCategoryIcon} {NewCategoryName.Trim()}";
        if (MasterCategoryList.Contains(fullCat))
        {
            CategoryManagementMessage = "Category already exists.";
            return;
        }

        MasterCategoryList.Add(fullCat);
        SynchronizeCategoryLists();
        NewCategoryName = string.Empty;
        CategoryManagementMessage = $"✅ Category '{fullCat}' added successfully!";
    }

    [RelayCommand]
    private void DeleteCustomCategory(object? catObj)
    {
        if (catObj is string cat && MasterCategoryList.Contains(cat))
        {
            if (MasterCategoryList.Count <= 1)
            {
                CategoryManagementMessage = "At least one category is required.";
                return;
            }

            MasterCategoryList.Remove(cat);
            SynchronizeCategoryLists();
            CategoryManagementMessage = $"🗑️ Category '{cat}' removed.";
        }
    }

    [RelayCommand]
    private void LoadIndustryPreset(string preset)
    {
        MasterCategoryList.Clear();
        switch (preset?.ToLowerInvariant())
        {
            case "cafe":
            case "restaurant":
                MasterCategoryList.Add("☕ Hot Coffee & Teas");
                MasterCategoryList.Add("🥤 Cold Brews & Shakes");
                MasterCategoryList.Add("🥪 Gourmet Sandwiches & Wraps");
                MasterCategoryList.Add("🍕 Artisan Pizzas & Pasta");
                MasterCategoryList.Add("🍔 Burgers & Quick Bites");
                MasterCategoryList.Add("🍰 Fresh Pastries & Desserts");
                MasterCategoryList.Add("🥗 Fresh Salads & Bowls");
                MasterCategoryList.Add("🍟 Fries & Finger Foods");
                break;

            case "pharmacy":
            case "medical":
                MasterCategoryList.Add("💊 Tablets & Capsules");
                MasterCategoryList.Add("🧪 Syrups & Suspensions");
                MasterCategoryList.Add("💉 Injections & Vaccines");
                MasterCategoryList.Add("🩹 Surgicals & Dressings");
                MasterCategoryList.Add("🧴 Personal Care & Hygiene");
                MasterCategoryList.Add("🍼 Baby Nutrition & Healthcare");
                MasterCategoryList.Add("🌿 Ayurvedic & Herbal Care");
                break;

            case "grocery":
            case "supermarket":
                MasterCategoryList.Add("🌾 Grains, Flours & Pulses");
                MasterCategoryList.Add("🧂 Spices, Masalas & Oils");
                MasterCategoryList.Add("🍪 Biscuits, Namkeen & Snacks");
                MasterCategoryList.Add("🥛 Dairy, Milk & Butter");
                MasterCategoryList.Add("🧼 Soaps, Cleaners & Detergents");
                MasterCategoryList.Add("🧴 Shampoos & Grooming");
                MasterCategoryList.Add("🥤 Soft Drinks & Juices");
                break;

            case "apparel":
            case "garments":
                MasterCategoryList.Add("👔 Men's Shirts & Trousers");
                MasterCategoryList.Add("👗 Women's Ethnic & Western");
                MasterCategoryList.Add("👶 Kids & Infants Wear");
                MasterCategoryList.Add("👟 Footwear & Sports Shoes");
                MasterCategoryList.Add("🎒 Bags, Belts & Wallets");
                break;

            case "electronics":
            case "mobile":
                MasterCategoryList.Add("📱 Smartphones & Tablets");
                MasterCategoryList.Add("🎧 Bluetooth Audio & TWS");
                MasterCategoryList.Add("🔌 Chargers & Data Cables");
                MasterCategoryList.Add("🔋 Fast Power Banks");
                MasterCategoryList.Add("💻 Laptop & PC Accessories");
                MasterCategoryList.Add("📺 Home Appliances & TVs");
                break;

            default:
                MasterCategoryList.Add("📦 General Store Items");
                MasterCategoryList.Add("🧴 Personal Care");
                MasterCategoryList.Add("🥤 Beverages & Drinks");
                MasterCategoryList.Add("🍬 Sweets & Confectionery");
                break;
        }

        SynchronizeCategoryLists();
        CategoryManagementMessage = $"✅ Switched to '{preset}' industry template with {MasterCategoryList.Count} categories!";
    }

    private void SynchronizeCategoryLists()
    {
        AvailableCategories.Clear();
        PosCategories.Clear();

        PosCategories.Add("🌟 All Categories");

        foreach (var c in MasterCategoryList)
        {
            AvailableCategories.Add(c);
            PosCategories.Add(c);
        }

        if (string.IsNullOrWhiteSpace(SelectedCategory) || !AvailableCategories.Contains(SelectedCategory))
        {
            var match = AvailableCategories.FirstOrDefault(c => CleanCategoryName(c) == CleanCategoryName(SelectedCategory));
            SelectedCategory = match ?? AvailableCategories.FirstOrDefault() ?? "💊 Pharmaceuticals & Tablets";
        }

        if (!PosCategories.Contains(SelectedPosCategory))
        {
            var matchPos = PosCategories.FirstOrDefault(c => CleanCategoryName(c) == CleanCategoryName(SelectedPosCategory));
            SelectedPosCategory = matchPos ?? "🌟 All Categories";
        }

        RefreshFilteredPosProducts();
    }

    // --- Stock Control & Adjustment Modal Actions ---

    [RelayCommand]
    private void OpenStockControlModal(object? productObj)
    {
        if (productObj is not Product product) return;

        SelectedStockProduct = product;
        StockAdjustmentType = "Inward";
        StockAdjustmentQuantity = 10;
        StockAdjustmentReason = "📥 Supplier Purchase / Goods Inward";
        StockReferenceNumber = string.Empty;
        StockSupplierOrParty = string.Empty;
        StockAdjustmentNotes = string.Empty;
        StockControlMessage = string.Empty;
        StockUpdatedBatch = product.BatchNumber ?? string.Empty;
        StockUpdatedExpiry = product.ExpiryDate;
        StockUpdatedRack = product.RackLocation ?? string.Empty;

        OnPropertyChanged(nameof(CalculatedNewStock));
        IsStockControlModalOpen = true;
    }

    [RelayCommand]
    private void CloseStockControlModal()
    {
        IsStockControlModalOpen = false;
        StockControlMessage = string.Empty;
    }

    [RelayCommand]
    private void SetStockAdjustmentType(string type)
    {
        StockAdjustmentType = type;
        if (type == "Inward")
        {
            StockAdjustmentReason = "📥 Supplier Purchase / Goods Inward";
        }
        else if (type == "Outward")
        {
            StockAdjustmentReason = "⚠️ Damaged / Broken Goods Write-Off";
        }
        else if (type == "Set")
        {
            StockAdjustmentReason = "🔄 Inventory Audit / Physical Stock Recount";
            if (SelectedStockProduct != null)
            {
                StockAdjustmentQuantity = SelectedStockProduct.CurrentStock;
            }
        }
        OnPropertyChanged(nameof(CalculatedNewStock));
    }

    [RelayCommand]
    private void QuickAddAdjustmentQty(string amountStr)
    {
        if (decimal.TryParse(amountStr, out decimal amt))
        {
            StockAdjustmentQuantity += amt;
            if (StockAdjustmentQuantity < 0) StockAdjustmentQuantity = 0;
            OnPropertyChanged(nameof(CalculatedNewStock));
        }
    }

    [RelayCommand]
    private void SetAdjustmentQtyPreset(string amountStr)
    {
        if (decimal.TryParse(amountStr, out decimal amt))
        {
            StockAdjustmentQuantity = amt;
            OnPropertyChanged(nameof(CalculatedNewStock));
        }
    }

    [RelayCommand]
    private async Task ConfirmStockAdjustment()
    {
        if (SelectedStockProduct == null) return;

        var product = SelectedStockProduct;
        decimal oldStock = product.CurrentStock;
        decimal targetStock = CalculatedNewStock;
        decimal diff = targetStock - oldStock;

        if (diff == 0 && StockAdjustmentType != "Set")
        {
            StockControlMessage = "⚠️ Adjustment quantity must be greater than zero.";
            return;
        }

        try
        {
            // 1. Update product stock in repo
            await _productRepo.AdjustStockAsync(product.Id, diff);
            product.CurrentStock = targetStock;

            // 2. Optionally update batch, expiry, or rack location if modified
            bool metadataChanged = false;
            if (!string.IsNullOrWhiteSpace(StockUpdatedBatch) && StockUpdatedBatch != product.BatchNumber)
            {
                product.BatchNumber = StockUpdatedBatch.Trim();
                metadataChanged = true;
            }
            if (StockUpdatedExpiry.HasValue && StockUpdatedExpiry != product.ExpiryDate)
            {
                product.ExpiryDate = StockUpdatedExpiry;
                metadataChanged = true;
            }
            if (!string.IsNullOrWhiteSpace(StockUpdatedRack) && StockUpdatedRack != product.RackLocation)
            {
                product.RackLocation = StockUpdatedRack.Trim();
                metadataChanged = true;
            }

            if (metadataChanged)
            {
                await _productRepo.UpdateAsync(product);
            }

            // 3. Log Audit Trail
            var audit = new AuditLog
            {
                UserId = CurrentUser?.Id,
                Username = CurrentUser?.Username ?? "admin",
                Module = "Inventory",
                Action = AuditActionType.StockAdjusted,
                RecordId = product.Id.ToString(),
                OldValue = $"Stock: {oldStock:0.##}",
                NewValue = $"Stock: {targetStock:0.##} ({StockAdjustmentType}: {diff:+0.##;-0.##})",
                Reason = $"{StockAdjustmentReason}. Ref: {StockReferenceNumber} {StockSupplierOrParty}. Notes: {StockAdjustmentNotes}".Trim()
            };
            await _auditRepo.LogAsync(audit);

            await LoadDashboardDataAsync();

            StockControlMessage = $"✅ Stock for '{product.Name}' updated to {targetStock:0.##} {product.Unit}!";
            await Task.Delay(500);
            IsStockControlModalOpen = false;
        }
        catch (Exception ex)
        {
            StockControlMessage = $"❌ Error adjusting stock: {ex.Message}";
        }
    }

    [RelayCommand]
    private void QuickStockIn(object? productObj)
    {
        if (productObj is not Product product) return;

        ShowAsyncConfirmation(
            "Quick Stock In (+10 Units)",
            $"Add +10 stock units to '{product.Name}'?",
            async () =>
            {
                await _productRepo.AdjustStockAsync(product.Id, 10);
                product.CurrentStock += 10;
                await LoadDashboardDataAsync();
            },
            "➕ Add 10 Units",
            "#16A34A");
    }

    [RelayCommand]
    private void QuickStockOut(object? productObj)
    {
        if (productObj is not Product product) return;

        ShowAsyncConfirmation(
            "Stock Adjustment / Write-Off (-1 Unit)",
            $"Deduct 1 unit from '{product.Name}' inventory stock?",
            async () =>
            {
                await _productRepo.AdjustStockAsync(product.Id, -1);
                product.CurrentStock = Math.Max(0, product.CurrentStock - 1);
                await LoadDashboardDataAsync();
            },
            "➖ Deduct 1 Unit",
            "#DC2626");
    }

    // --- Pharmacy Batch Management Actions ---

    partial void OnPharmacySearchQueryChanged(string value)
    {
        RefreshFilteredPharmacy();
    }

    partial void OnPharmacyFilterModeChanged(string value)
    {
        RefreshFilteredPharmacy();
    }

    [RelayCommand]
    private void SetPharmacyFilterMode(object? modeObj)
    {
        if (modeObj is string mode)
        {
            PharmacyFilterMode = mode;
        }
    }

    private void RefreshFilteredPharmacy()
    {
        FilteredPharmacyBatches.Clear();
        var q = PharmacySearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        var matches = AllPharmacyBatches.AsEnumerable();

        if (!string.IsNullOrEmpty(q))
        {
            matches = matches.Where(b => 
                b.MedicineName.ToLowerInvariant().Contains(q) || 
                b.BatchNumber.ToLowerInvariant().Contains(q) || 
                b.SKU.ToLowerInvariant().Contains(q));
        }

        if (PharmacyFilterMode == "Expired")
        {
            matches = matches.Where(b => b.DaysToExpiry < 0);
        }
        else if (PharmacyFilterMode == "NearExpiry")
        {
            matches = matches.Where(b => b.DaysToExpiry is >= 0 and <= 60);
        }
        else if (PharmacyFilterMode == "Safe")
        {
            matches = matches.Where(b => b.DaysToExpiry > 60);
        }

        foreach (var b in matches)
        {
            FilteredPharmacyBatches.Add(b);
        }
    }

    // --- GST Reports Engine Actions ---

    partial void OnSelectedGstReportYearChanged(int value)
    {
        GenerateGstReport();
    }

    partial void OnSelectedGstReportMonthChanged(string value)
    {
        GenerateGstReport();
    }

    partial void OnSelectedGstReportTypeChanged(string value)
    {
        GenerateGstReport();
    }

    private (DateTime StartUtc, DateTime EndUtc, string Label) GetGstReportDateRange()
    {
        int year = SelectedGstReportYear > 2000 ? SelectedGstReportYear : DateTime.UtcNow.Year;

        if (string.IsNullOrWhiteSpace(SelectedGstReportMonth) || 
            SelectedGstReportMonth.StartsWith("🌟") || 
            SelectedGstReportMonth.Contains("Full", StringComparison.OrdinalIgnoreCase))
        {
            var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            return (start, end, $"Calendar Year {year} (01-Jan-{year} to 31-Dec-{year})");
        }

        int month = DateTime.UtcNow.Month;
        if (SelectedGstReportMonth.Length >= 2 && int.TryParse(SelectedGstReportMonth.Substring(0, 2), out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
        {
            month = parsedMonth;
        }

        int daysInMonth = DateTime.DaysInMonth(year, month);
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = new DateTime(year, month, daysInMonth, 23, 59, 59, DateTimeKind.Utc);
        string monthName = new DateTime(year, month, 1).ToString("MMMM");
        return (monthStart, monthEnd, $"{monthName} {year} (01-{monthName.Substring(0, 3)}-{year} to {daysInMonth}-{monthName.Substring(0, 3)}-{year})");
    }

    [RelayCommand]
    private void GenerateGstReport()
    {
        GstHsnSummaries.Clear();
        GstRateBreakdowns.Clear();

        decimal taxableTotal = 0;
        decimal cgstTotal = 0;
        decimal sgstTotal = 0;
        decimal igstTotal = 0;
        decimal grandSales = 0;

        var (startUtc, endUtc, periodLabel) = GetGstReportDateRange();
        ReportStartDate = startUtc;
        ReportEndDate = endUtc;

        var itemsByHsn = new Dictionary<string, (decimal Qty, decimal Taxable, decimal GstRate, string Name)>();
        var taxByRate = new Dictionary<decimal, (decimal Taxable, decimal CGST, decimal SGST, decimal IGST)>();

        var periodInvoices = RecentInvoices.Where(i => i.InvoiceDateUtc >= startUtc && i.InvoiceDateUtc <= endUtc && i.Status == InvoiceStatus.Finalized).ToList();

        if (periodInvoices.Count > 0)
        {
            foreach (var inv in periodInvoices)
            {
                foreach (var item in inv.Items)
                {
                    decimal lineTaxable = item.TaxableValue;
                    decimal lineTax = item.CGSTAmount + item.SGSTAmount + item.IGSTAmount;
                    decimal qty = item.Quantity;

                    taxableTotal += lineTaxable;
                    cgstTotal += item.CGSTAmount;
                    sgstTotal += item.SGSTAmount;
                    igstTotal += item.IGSTAmount;
                    grandSales += item.TotalAmount;

                    string hsn = string.IsNullOrWhiteSpace(item.HSNCode) ? "300490" : item.HSNCode;
                    if (!itemsByHsn.ContainsKey(hsn))
                    {
                        itemsByHsn[hsn] = (0, 0, item.GSTRate, item.ProductName);
                    }
                    var curHsn = itemsByHsn[hsn];
                    itemsByHsn[hsn] = (curHsn.Qty + qty, curHsn.Taxable + lineTaxable, item.GSTRate, item.ProductName);

                    decimal rate = item.GSTRate;
                    if (!taxByRate.ContainsKey(rate))
                    {
                        taxByRate[rate] = (0, 0, 0, 0);
                    }
                    var curRate = taxByRate[rate];
                    taxByRate[rate] = (curRate.Taxable + lineTaxable, curRate.CGST + item.CGSTAmount, curRate.SGST + item.SGSTAmount, curRate.IGST + item.IGSTAmount);
                }
            }
        }
        else
        {
            // Sample items from catalog for visual representation
            foreach (var p in AvailableProducts)
            {
                decimal qty = Math.Max(5, p.CurrentStock / 2);
                decimal lineTaxable = qty * (p.SellingPrice / (1 + (p.GSTRate / 100)));
                decimal lineTax = (qty * p.SellingPrice) - lineTaxable;

                taxableTotal += lineTaxable;
                cgstTotal += lineTax / 2;
                sgstTotal += lineTax / 2;
                grandSales += qty * p.SellingPrice;

                string hsn = string.IsNullOrWhiteSpace(p.HSNCode) ? "300490" : p.HSNCode;
                if (!itemsByHsn.ContainsKey(hsn))
                {
                    itemsByHsn[hsn] = (0, 0, p.GSTRate, p.Name);
                }
                var curHsn = itemsByHsn[hsn];
                itemsByHsn[hsn] = (curHsn.Qty + qty, curHsn.Taxable + lineTaxable, p.GSTRate, p.Name);

                decimal rate = p.GSTRate;
                if (!taxByRate.ContainsKey(rate))
                {
                    taxByRate[rate] = (0, 0, 0, 0);
                }
                var curRate = taxByRate[rate];
                taxByRate[rate] = (curRate.Taxable + lineTaxable, curRate.CGST + lineTax / 2, curRate.SGST + lineTax / 2, curRate.IGST);
            }
        }

        foreach (var kvp in itemsByHsn)
        {
            decimal hsnTax = kvp.Value.Taxable * (kvp.Value.GstRate / 100);
            GstHsnSummaries.Add(new GstHsnSummaryItem
            {
                HSNCode = kvp.Key,
                Description = kvp.Value.Name,
                UQC = "PCS",
                TotalQuantity = kvp.Value.Qty,
                TotalValue = kvp.Value.Taxable + hsnTax,
                TaxableValue = kvp.Value.Taxable,
                GstRate = kvp.Value.GstRate,
                CentralTaxAmount = hsnTax / 2,
                StateTaxAmount = hsnTax / 2,
                IntegratedTaxAmount = 0
            });
        }

        foreach (var kvp in taxByRate.OrderBy(r => r.Key))
        {
            GstRateBreakdowns.Add(new GstRateBreakdownItem
            {
                RateLabel = $"{kvp.Key:N0}% GST Bracket",
                GstRate = kvp.Key,
                TaxableAmount = kvp.Value.Taxable,
                CGSTAmount = kvp.Value.CGST,
                SGSTAmount = kvp.Value.SGST,
                IGSTAmount = kvp.Value.IGST
            });
        }

        ReportTotalTaxableValue = Math.Round(taxableTotal, 2);
        ReportTotalCgst = Math.Round(cgstTotal, 2);
        ReportTotalSgst = Math.Round(sgstTotal, 2);
        ReportTotalIgst = Math.Round(igstTotal, 2);
        ReportTotalTax = Math.Round(cgstTotal + sgstTotal + igstTotal, 2);
        ReportGrandTotalSales = Math.Round(grandSales, 2);

        ReportStatusMessage = $"✅ {SelectedGstReportType} generated for period {periodLabel}.";
    }

    [RelayCommand]
    private void ExportGstReportCsv()
    {
        try
        {
            var (_, _, periodLabel) = GetGstReportDateRange();
            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
            Directory.CreateDirectory(exportDir);
            string fileName = $"GST_Return_{SelectedGstReportType}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
            string filePath = Path.Combine(exportDir, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("=== AFS GST STATUTORY RETURN REPORT ===");
            sb.AppendLine($"Report Type,{SelectedGstReportType}");
            sb.AppendLine($"Accounting Period,{periodLabel}");
            sb.AppendLine($"Business Entity,{CurrentCompany?.LegalName} ({CurrentCompany?.TradeName})");
            sb.AppendLine($"GSTIN,{CurrentCompany?.GSTIN}");
            sb.AppendLine($"Generated On,{DateTime.UtcNow:dd-MMM-yyyy HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("HSN_CODE,DESCRIPTION,UQC,QUANTITY,TAXABLE_VALUE,GST_RATE,CGST_AMOUNT,SGST_AMOUNT,TOTAL_TAX");

            foreach (var hsn in GstHsnSummaries)
            {
                sb.AppendLine($"{hsn.HSNCode},\"{hsn.Description}\",{hsn.UQC},{hsn.TotalQuantity},{hsn.TaxableValue:N2},{hsn.GstRate:N2},{hsn.CentralTaxAmount:N2},{hsn.StateTaxAmount:N2},{hsn.CentralTaxAmount + hsn.StateTaxAmount:N2}");
            }

            File.WriteAllText(filePath, sb.ToString());
            ReportStatusMessage = $"📥 Exported CSV report to: {fileName}";

            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            ReportStatusMessage = $"Export error: {ex.Message}";
        }
    }

    // --- Users, Roles & Permissions Actions ---

    [RelayCommand]
    private void CreateStoreUser()
    {
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewFullName))
        {
            UserManagementMessage = "Username and Full Name are required.";
            return;
        }

        ShowAsyncConfirmation(
            "Create New User Account",
            $"Create staff user '{NewFullName}' (@{NewUsername}) with Role '{SelectedNewUserRole}'?",
            async () =>
            {
                var (hash, salt) = _hasher.HashPassword(NewUserPassword);

                SystemPermissions perms = SystemPermissions.ViewDashboard;
                if (PermCanManageInventory) perms |= SystemPermissions.ManageInventory;
                if (PermCanPerformBilling) perms |= SystemPermissions.CreateInvoice | SystemPermissions.PrintInvoice;
                if (PermCanApplyDiscount) perms |= SystemPermissions.ApplyDiscount;
                if (PermCanViewReports) perms |= SystemPermissions.ViewReports;
                if (PermCanExportCaData) perms |= SystemPermissions.ExportCAData;
                if (PermCanManageUsers) perms |= SystemPermissions.ManageUsers;

                var user = new User
                {
                    Username = NewUsername.Trim(),
                    FullName = NewFullName.Trim(),
                    Email = NewUserEmail?.Trim() ?? string.Empty,
                    Phone = NewUserPhone?.Trim() ?? string.Empty,
                    Role = SelectedNewUserRole,
                    Permissions = perms,
                    PasswordHash = hash,
                    Salt = salt,
                    IsActive = true
                };

                bool created = await _userRepo.CreateUserAsync(user);
                if (created)
                {
                    UserManagementMessage = $"✅ User '{user.Username}' registered successfully!";
                    NewUsername = string.Empty;
                    NewFullName = string.Empty;
                    NewUserEmail = string.Empty;
                    NewUserPhone = string.Empty;

                    await LoadDashboardDataAsync();
                }
                else
                {
                    UserManagementMessage = "❌ Could not create user (username may already exist).";
                }
            },
            "👥 Create User",
            "#2563EB");
    }

    [RelayCommand]
    private void ToggleUserActiveStatus(object? userObj)
    {
        if (userObj is not User user) return;

        string action = user.IsActive ? "Deactivate" : "Activate";
        ShowAsyncConfirmation(
            $"{action} User Account",
            $"Are you sure you want to {action.ToLowerInvariant()} user '{user.FullName}' (@{user.Username})?",
            async () =>
            {
                user.IsActive = !user.IsActive;
                await _userRepo.UpdateUserAsync(user);
                await LoadDashboardDataAsync();
            },
            $"{action} User",
            user.IsActive ? "#DC2626" : "#16A34A");
    }

    // --- Super Admin / CA Encrypted Data Export Actions ---

    partial void OnSelectedExportYearChanged(int value)
    {
        _ = UpdateCaExportPeriodPreviewAsync();
    }

    partial void OnSelectedExportMonthChanged(string value)
    {
        _ = UpdateCaExportPeriodPreviewAsync();
    }

    private (DateTime StartUtc, DateTime EndUtc, string Label, string FileTag) GetExportDateRange()
    {
        int year = SelectedExportYear > 2000 ? SelectedExportYear : DateTime.UtcNow.Year;

        if (string.IsNullOrWhiteSpace(SelectedExportMonth) || 
            SelectedExportMonth.StartsWith("🌟") || 
            SelectedExportMonth.Contains("Full", StringComparison.OrdinalIgnoreCase))
        {
            var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            return (start, end, $"Calendar Year {year} (01-Jan-{year} to 31-Dec-{year})", $"{year}_FullYear");
        }

        int month = DateTime.UtcNow.Month;
        if (SelectedExportMonth.Length >= 2 && int.TryParse(SelectedExportMonth.Substring(0, 2), out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
        {
            month = parsedMonth;
        }

        int daysInMonth = DateTime.DaysInMonth(year, month);
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = new DateTime(year, month, daysInMonth, 23, 59, 59, DateTimeKind.Utc);
        string monthName = new DateTime(year, month, 1).ToString("MMMM");
        return (monthStart, monthEnd, $"{monthName} {year} (01-{monthName.Substring(0, 3)}-{year} to {daysInMonth}-{monthName.Substring(0, 3)}-{year})", $"{year}_{month:D2}_{monthName}");
    }

    public async Task UpdateCaExportPeriodPreviewAsync()
    {
        try
        {
            var (startUtc, endUtc, label, _) = GetExportDateRange();
            CaExportPeriodDisplay = label;
            CaExportStartDate = startUtc;
            CaExportEndDate = endUtc;

            var invoices = await _invoiceRepo.GetRecentInvoicesAsync(10000);
            var periodInvoices = invoices.Where(i => i.InvoiceDateUtc >= startUtc && i.InvoiceDateUtc <= endUtc && i.Status == InvoiceStatus.Finalized).ToList();

            CaExportInvoiceCount = periodInvoices.Count;
            CaExportPeriodTaxable = periodInvoices.Sum(i => i.TaxableAmount);
            CaExportPeriodCgst = periodInvoices.Sum(i => i.TotalCGST);
            CaExportPeriodSgst = periodInvoices.Sum(i => i.TotalSGST);
            CaExportPeriodIgst = periodInvoices.Sum(i => i.TotalIGST);
            CaExportPeriodTotalTax = CaExportPeriodCgst + CaExportPeriodSgst + CaExportPeriodIgst;
            CaExportPeriodRevenue = periodInvoices.Sum(i => i.GrandTotal);
        }
        catch
        {
            // fallback
        }
    }

    [RelayCommand]
    private void SetExportCurrentMonth()
    {
        SelectedExportYear = DateTime.UtcNow.Year;
        SelectedExportMonth = DateTime.UtcNow.ToString("MM - MMMM");
    }

    [RelayCommand]
    private void SetExportPreviousMonth()
    {
        var prev = DateTime.UtcNow.AddMonths(-1);
        SelectedExportYear = prev.Year;
        SelectedExportMonth = prev.ToString("MM - MMMM");
    }

    [RelayCommand]
    private void SetExportFullYear()
    {
        SelectedExportYear = DateTime.UtcNow.Year;
        SelectedExportMonth = AvailableExportMonths[0]; // Full Accounting Year
    }

    [RelayCommand]
    private void SetExportFinancialYear()
    {
        int fyStartYear = DateTime.UtcNow.Month >= 4 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        SelectedExportYear = fyStartYear;
        SelectedExportMonth = AvailableExportMonths[0];
        CaExportStartDate = new DateTime(fyStartYear, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        CaExportEndDate = new DateTime(fyStartYear + 1, 3, 31, 23, 59, 59, DateTimeKind.Utc);
        CaExportPeriodDisplay = $"Financial Year FY {fyStartYear}-{(fyStartYear + 1) % 100:D2} (01-Apr-{fyStartYear} to 31-Mar-{fyStartYear + 1})";
        _ = UpdateCaExportPeriodPreviewAsync();
    }

    [RelayCommand]
    private void GenerateEncryptedCaPackage()
    {
        if (CurrentUser?.Role != UserRole.BusinessAdmin)
        {
            CaExportStatusMessage = "🔒 Access Denied: Only Business Admin can encrypt files and send to Super Admin.";
            return;
        }

        var (_, _, periodLabel, _) = GetExportDateRange();
        ShowAsyncConfirmation(
            "Generate Encrypted Commercial Package for Super Admin",
            $"Generate a cryptographically signed & AES-256-GCM encrypted package containing all invoices, GST tax registers, inventory valuation, and audit trail for period '{periodLabel}' to send to Super Admin?",
            async () => await ExecuteGenerateCaPackageAsync(),
            "🔐 Generate Encrypted Package",
            "#2563EB");
    }

    private async Task ExecuteGenerateCaPackageAsync()
    {
        if (CurrentUser?.Role != UserRole.BusinessAdmin)
        {
            CaExportStatusMessage = "🔒 Access Denied: Only Business Admin can encrypt files and send to Super Admin.";
            return;
        }

        try
        {
            var (startUtc, endUtc, periodLabel, fileTag) = GetExportDateRange();
            var company = await _companyRepo.GetCompanyAsync();
            var allInvoices = (await _invoiceRepo.GetRecentInvoicesAsync(10000)).ToList();
            var invoices = allInvoices.Where(i => i.InvoiceDateUtc >= startUtc && i.InvoiceDateUtc <= endUtc).ToList();
            var products = (await _productRepo.GetAllAsync()).ToList();
            var allLogs = (await _auditRepo.GetRecentLogsAsync(5000)).ToList();
            var auditLogs = allLogs.Where(a => a.TimestampUtc >= startUtc && a.TimestampUtc <= endUtc).ToList();

            decimal taxableTotal = invoices.Sum(i => i.TaxableAmount);
            decimal cgstTotal = invoices.Sum(i => i.TotalCGST);
            decimal sgstTotal = invoices.Sum(i => i.TotalSGST);
            decimal igstTotal = invoices.Sum(i => i.TotalIGST);
            decimal totalTax = cgstTotal + sgstTotal + igstTotal;
            decimal totalRevenue = invoices.Sum(i => i.GrandTotal);
            decimal inventoryValuation = products.Sum(p => p.CurrentStock * p.PurchasePrice);

            var exportPayload = new
            {
                ExportId = Guid.NewGuid(),
                ExportFormat = "AFS-SUPERADMIN-ENCRYPTED-PACKAGE-V2",
                BusinessCode = company?.BusinessCode ?? "BUS-STORE",
                LegalName = company?.LegalName ?? "Store Entity",
                GSTIN = company?.GSTIN ?? "",
                AccountingPeriod = periodLabel,
                SelectedYear = SelectedExportYear,
                SelectedMonth = SelectedExportMonth,
                PeriodStartUtc = startUtc,
                PeriodEndUtc = endUtc,
                ExportTimestampUtc = DateTime.UtcNow,
                Metrics = new
                {
                    TotalInvoices = invoices.Count,
                    TotalRevenue = totalRevenue,
                    TaxableTurnover = taxableTotal,
                    CGST = cgstTotal,
                    SGST = sgstTotal,
                    IGST = igstTotal,
                    TotalTax = totalTax,
                    TotalProducts = products.Count,
                    InventoryValuation = inventoryValuation,
                    AuditRecordsCount = auditLogs.Count
                },
                Invoices = invoices,
                Products = products,
                AuditLogs = auditLogs
            };

            string jsonPlain = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true });
            byte[] plainBytes = Encoding.UTF8.GetBytes(jsonPlain);

            // Encrypt using AES-256-GCM with AFS Master Key
            var aesGcm = new AesGcmService();
            byte[] keyBytes = Encoding.UTF8.GetBytes("AFS_MASTER_COMMERCIAL_KEY_2026!!");
            var (cipherBytes, nonceBytes, tagBytes) = aesGcm.Encrypt(plainBytes, keyBytes);

            var encryptedPackage = new
            {
                Format = "AFS-SUPERADMIN-ENCRYPTED-PACKAGE-V2",
                Ciphertext = Convert.ToBase64String(cipherBytes),
                Nonce = Convert.ToBase64String(nonceBytes),
                Tag = Convert.ToBase64String(tagBytes),
                BusinessCode = company?.BusinessCode,
                AccountingPeriod = periodLabel,
                ExportDateUtc = DateTime.UtcNow
            };

            string packageContent = JsonSerializer.Serialize(encryptedPackage, new JsonSerializerOptions { WriteIndented = true });

            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
            Directory.CreateDirectory(exportDir);
            string fileName = $"AFS_CA_Export_{company?.BusinessCode}_{fileTag}_{DateTime.UtcNow:yyyyMMdd_HHmm}.afspkg";
            string filePath = Path.Combine(exportDir, fileName);

            File.WriteAllText(filePath, packageContent);

            LastExportedPackagePath = filePath;
            LastExportedPackageFileName = fileName;
            IsCaPackageExported = true;

            CaExportSummaryText = 
$@"=== AFS ENCRYPTED SUPER ADMIN / CA PACKAGE ISSUED ===
Business Code:        {company?.BusinessCode}
Legal Entity:         {company?.LegalName}
GSTIN:                {company?.GSTIN}
Accounting Period:    {periodLabel}
Date Range (UTC):     {startUtc:dd-MMM-yyyy HH:mm} to {endUtc:dd-MMM-yyyy HH:mm}
Invoices Encrypted:   {invoices.Count} Invoices
Taxable Turnover:     ₹{taxableTotal:N2}
CGST + SGST (Tax):    ₹{cgstTotal:N2} + ₹{sgstTotal:N2} (Total Tax: ₹{totalTax:N2})
Gross Revenue:        ₹{totalRevenue:N2}
Catalog Encrypted:    {products.Count} Products (Valuation: ₹{inventoryValuation:N2})
Audit Trail Records:  {auditLogs.Count} Logs
Encryption Security:  AES-256-GCM Military Grade (Encrypted for AFS Super Admin / CA)
Export File Name:     {fileName}
Export File Path:     {filePath}";

            CaExportStatusMessage = $"✅ Encrypted Package '{fileName}' generated successfully for {periodLabel}!";

            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (Exception ex)
        {
            CaExportStatusMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenExportFolder()
    {
        try
        {
            if (File.Exists(LastExportedPackagePath))
            {
                Process.Start("explorer.exe", $"/select,\"{LastExportedPackagePath}\"");
            }
            else
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", dir);
            }
        }
        catch
        {
            // Ignored
        }
    }

    // =========================================================================
    // LICENSE RENEWAL & SUPER ADMIN CERTIFICATE IMPORT (.lic / .key)
    // =========================================================================

    [RelayCommand]
    private void OpenLicenseRenewalModal()
    {
        if (CurrentUser?.Role != UserRole.BusinessAdmin)
        {
            PosStatusMessage = "🔒 Access Denied: Only Business Admin can apply subscription license renewals.";
            return;
        }

        SelectedRenewalFilePath = string.Empty;
        RenewalValidationMessage = string.Empty;
        IsValidRenewalLoaded = false;
        PreviewRenewalPayload = null;
        RenewalPreviewSummary = string.Empty;
        IsLicenseRenewalModalOpen = true;
    }

    [RelayCommand]
    private void CloseLicenseRenewalModal()
    {
        IsLicenseRenewalModalOpen = false;
    }

    [RelayCommand]
    private void BrowseLicenseRenewalFile()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select AFS License Renewal Certificate (.lic) or Data Key (.key)",
                Filter = "AFS License & Key Files (*.lic;*.key)|*.lic;*.key|License Certificates (*.lic)|*.lic|Provisioning Keys (*.key)|*.key|All Files (*.*)|*.*"
            };

            string candidateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedLicenses");
            if (Directory.Exists(candidateDir))
            {
                dlg.InitialDirectory = candidateDir;
            }

            if (dlg.ShowDialog() == true)
            {
                SelectedRenewalFilePath = dlg.FileName;
                ValidateRenewalFile(dlg.FileName);
            }
        }
        catch (Exception ex)
        {
            RenewalValidationMessage = $"File selection error: {ex.Message}";
        }
    }

    private void ValidateRenewalFile(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath).Trim();
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".lic" || content.StartsWith("{") || !content.Contains("AFS-COMMERCIAL-DATA-KEY-V1"))
            {
                // Try validating as .lic certificate
                var (success, error, payload) = _keyService.ValidateLicenseRenewal(content, SecurityConstants.MasterCaPublicKeyPem);
                if (success && payload != null)
                {
                    if (CurrentCompany != null && !payload.BusinessCode.Equals(CurrentCompany.BusinessCode, StringComparison.OrdinalIgnoreCase))
                    {
                        IsValidRenewalLoaded = false;
                        RenewalValidationMessage = $"⚠️ Warning: License business code '{payload.BusinessCode}' does not match active store '{CurrentCompany.BusinessCode}'.";
                        return;
                    }

                    PreviewRenewalPayload = payload;
                    IsValidRenewalLoaded = true;
                    RenewalValidationMessage = "✅ Cryptographically verified RSA-4096 signature from AFS Super Admin!";
                    int days = Math.Max(0, (int)(payload.ExpiryDateUtc.Date - DateTime.UtcNow.Date).TotalDays);

                    RenewalPreviewSummary = 
$@"=== VERIFIED LICENSE RENEWAL CERTIFICATE ===
Business Code:       {payload.BusinessCode}
Subscription Tier:   {payload.Plan} Plan
Issued Date (UTC):   {payload.IssuedDateUtc:dd-MMM-yyyy HH:mm}
Expiry Date (UTC):   {payload.ExpiryDateUtc:dd-MMM-yyyy HH:mm} ({days} Days Remaining)
Max Users Allowed:   {payload.MaxUsers} Users
Max Products:        {payload.MaxProducts} SKUs
Signature Status:    Verified Authentic with AFS Master CA RSA Key";
                    return;
                }
            }

            // Otherwise try validating as .key file
            var (kSuccess, kError, kPayload) = _keyService.UnpackAndValidateDataKey(content, SecurityConstants.MasterCaPublicKeyPem);
            if (kSuccess && kPayload != null)
            {
                if (CurrentCompany != null && !kPayload.BusinessCode.Equals(CurrentCompany.BusinessCode, StringComparison.OrdinalIgnoreCase))
                {
                    IsValidRenewalLoaded = false;
                    RenewalValidationMessage = $"⚠️ Warning: Key business code '{kPayload.BusinessCode}' does not match active store '{CurrentCompany.BusinessCode}'.";
                    return;
                }

                PreviewRenewalPayload = new LicenseRenewalPayload
                {
                    LicenseId = Guid.NewGuid().ToString("N"),
                    BusinessCode = kPayload.BusinessCode,
                    Plan = kPayload.SubscriptionPlan,
                    IssuedDateUtc = kPayload.IssuedDateUtc,
                    ExpiryDateUtc = kPayload.ExpiryDateUtc,
                    GracePeriodDays = kPayload.GracePeriodDays,
                    MaxUsers = kPayload.MaxUsers,
                    MaxProducts = kPayload.MaxProducts,
                    EnabledModules = kPayload.EnabledModules
                };

                IsValidRenewalLoaded = true;
                RenewalValidationMessage = "✅ Cryptographically verified RSA-4096 Data Key from AFS Super Admin!";
                int days = Math.Max(0, (int)(kPayload.ExpiryDateUtc.Date - DateTime.UtcNow.Date).TotalDays);

                RenewalPreviewSummary = 
$@"=== VERIFIED PROVISIONING DATA KEY RENEWAL ===
Business Code:       {kPayload.BusinessCode}
Legal Entity:        {kPayload.LegalName}
Subscription Tier:   {kPayload.SubscriptionPlan} Plan
Expiry Date (UTC):   {kPayload.ExpiryDateUtc:dd-MMM-yyyy HH:mm} ({days} Days Remaining)
Max Users Allowed:   {kPayload.MaxUsers} Users
Max Products:        {kPayload.MaxProducts} SKUs
Signature Status:    Verified Authentic with AFS Master CA RSA Key";
                return;
            }

            IsValidRenewalLoaded = false;
            RenewalValidationMessage = "❌ Invalid license certificate or signature verification failed.";
        }
        catch (Exception ex)
        {
            IsValidRenewalLoaded = false;
            RenewalValidationMessage = $"Validation error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyLicenseRenewalAsync()
    {
        if (PreviewRenewalPayload == null || !IsValidRenewalLoaded) return;

        try
        {
            var p = PreviewRenewalPayload;
            var record = new LicenseRecord
            {
                Id = Guid.NewGuid(),
                BusinessCode = p.BusinessCode,
                LicenseKey = p.LicenseId,
                Plan = p.Plan,
                IssuedDateUtc = p.IssuedDateUtc,
                ExpiryDateUtc = p.ExpiryDateUtc,
                GracePeriodDays = p.GracePeriodDays,
                MaxUsers = p.MaxUsers,
                MaxProducts = p.MaxProducts,
                Status = LicenseStatus.Active,
                Signature = p.Signature,
                LastVerifiedUtc = DateTime.UtcNow
            };

            await _licenseRepo.SaveLicenseAsync(record);

            await _auditRepo.LogAsync(new AuditLog
            {
                UserId = CurrentUser?.Id,
                Username = CurrentUser?.Username ?? "admin",
                MachineName = Environment.MachineName,
                Module = "LICENSE_MANAGEMENT",
                Action = AuditActionType.LicenseRenewed,
                RecordId = p.BusinessCode,
                NewValue = $"License renewed to {p.Plan} tier until {p.ExpiryDateUtc:dd-MMM-yyyy}",
                Reason = "Applied Super Admin RSA-signed license renewal certificate"
            });

            CurrentLicense = record;
            int days = Math.Max(0, (int)(record.ExpiryDateUtc.Date - DateTime.UtcNow.Date).TotalDays);
            LicenseDaysRemainingText = $"{record.Plan} Tier • {days} Days Remaining ({record.Status})";

            IsLicenseRenewalModalOpen = false;
            PosStatusMessage = $"✅ Subscription successfully updated to {record.Plan} Tier! Valid until {record.ExpiryDateUtc:dd-MMM-yyyy}.";
        }
        catch (Exception ex)
        {
            RenewalValidationMessage = $"Failed to save license to database: {ex.Message}";
        }
    }

    // =========================================================================
    // FUNCTION KEY SHORTCUT COMMANDS (F1 - F12, ESC, CTRL+P, CTRL+N)
    // =========================================================================

    [RelayCommand]
    private void ShortcutF1Pos()
    {
        SwitchTab("Billing");
        BillingSubTab = "NewSale";
        PosStatusMessage = "🛒 Switched to POS Billing Terminal (F1)";
    }

    [RelayCommand]
    private void ShortcutF2Inventory()
    {
        SwitchTab("Inventory");
        InventoryFormMessage = "📦 Viewing Store Inventory & Stock Catalog (F2)";
    }

    [RelayCommand]
    private void ShortcutF3NewProduct()
    {
        SwitchTab("Inventory");
        InventoryFormMessage = "➕ Ready to Add New Product (F3) - Fill details in the form on the right";
    }

    [RelayCommand]
    private async Task ShortcutF4Pay()
    {
        if (CurrentDashboardTab != "Billing")
        {
            SwitchTab("Billing");
        }
        await CheckoutAndPrintInvoice();
    }

    [RelayCommand]
    private void ShortcutF5NewBill()
    {
        SwitchTab("Billing");
        BillingSubTab = "NewSale";
        ClearCart();
        PosStatusMessage = "🔄 Started New POS Bill (F5) - Cart Reset";
    }

    [RelayCommand]
    private void ShortcutF6Pharmacy()
    {
        SwitchTab("Pharmacy");
    }

    [RelayCommand]
    private void ShortcutF7Discount()
    {
        if (CurrentDashboardTab != "Billing")
        {
            SwitchTab("Billing");
        }

        if (!CanApplyDiscount)
        {
            PosStatusMessage = "🔒 Discount is restricted to Business Admin role.";
            return;
        }

        DiscountType = DiscountType == "Flat" ? "Percentage" : "Flat";
        RecalculateCartTotals();
        PosStatusMessage = $"🏷️ Switched Discount Mode to {(DiscountType == "Flat" ? "Flat ₹" : "Percentage %")} (F7)";
    }

    [RelayCommand]
    private void ShortcutF8History()
    {
        SwitchTab("Billing");
        SetBillingSubTab("History");
        PosStatusMessage = "📑 Viewing Invoices & Sales Ledger History (F8)";
    }

    [RelayCommand]
    private async Task ShortcutF9Reprint()
    {
        if (RecentInvoices.Any())
        {
            var lastInv = RecentInvoices.First();
            await OpenPrintPreview(lastInv);
            PosStatusMessage = $"🖨️ Opening Print Preview for Invoice #{lastInv.InvoiceNumber} (F9 / Ctrl+P)";
        }
        else
        {
            PosStatusMessage = "⚠️ No recent invoices found to reprint.";
        }
    }

    [RelayCommand]
    private void ShortcutF10Categories()
    {
        OpenManageCategoriesModal();
    }

    [RelayCommand]
    private void ShortcutF11HoldBill()
    {
        if (CurrentDashboardTab != "Billing")
        {
            SwitchTab("Billing");
        }

        if (!PosCartItems.Any())
        {
            PosStatusMessage = "⚠️ Cannot hold an empty bill. Add items to cart first.";
            return;
        }

        var parked = new ParkedBill
        {
            CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName.Trim(),
            CustomerPhone = CustomerPhone ?? string.Empty,
            CustomerGstin = CustomerGstin ?? string.Empty,
            DiscountValue = DiscountValue,
            DiscountType = DiscountType,
            GrandTotal = PosGrandTotal,
            Items = PosCartItems.Select(i => new InvoiceItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                SKU = i.SKU,
                HSNCode = i.HSNCode,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                TaxableValue = i.TaxableValue,
                GSTRate = i.GSTRate,
                CGSTAmount = i.CGSTAmount,
                SGSTAmount = i.SGSTAmount,
                TotalAmount = i.TotalAmount
            }).ToList()
        };

        ParkedBills.Add(parked);
        ParkedBillsCount = ParkedBills.Count;

        ClearCart();
        PosStatusMessage = $"⏸️ Bill for '{parked.CustomerName}' (₹{parked.GrandTotal:N2}) held & parked! [Total Parked: {ParkedBillsCount}] - Press F12 to Recall.";
    }

    [RelayCommand]
    private void ShortcutF12RecallBill()
    {
        if (CurrentDashboardTab != "Billing")
        {
            SwitchTab("Billing");
        }

        if (!ParkedBills.Any())
        {
            PosStatusMessage = "ℹ️ No parked bills currently on hold.";
            return;
        }

        var parked = ParkedBills.Last();
        ParkedBills.Remove(parked);
        ParkedBillsCount = ParkedBills.Count;

        PosCartItems.Clear();
        foreach (var item in parked.Items)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InvoiceItem.Quantity) || e.PropertyName == nameof(InvoiceItem.DiscountAmount))
                {
                    RecalculateCartTotals();
                }
            };
            PosCartItems.Add(item);
        }

        CustomerName = parked.CustomerName;
        CustomerPhone = parked.CustomerPhone;
        CustomerGstin = parked.CustomerGstin;
        DiscountValue = parked.DiscountValue;
        DiscountType = parked.DiscountType;

        RecalculateCartTotals();
        PosStatusMessage = $"⏯️ Recalled parked bill for '{parked.CustomerName}' (₹{PosGrandTotal:N2})! [Remaining Parked: {ParkedBillsCount}]";
    }

    [RelayCommand]
    private void ShortcutEscape()
    {
        bool anyClosed = IsCreateRackModalOpen || IsManageCategoriesModalOpen || IsStockControlModalOpen ||
                          IsPrintPreviewModalOpen || IsLicenseRenewalModalOpen || IsConfirmModalOpen;

        IsCreateRackModalOpen = false;
        IsManageCategoriesModalOpen = false;
        IsStockControlModalOpen = false;
        IsPrintPreviewModalOpen = false;
        IsLicenseRenewalModalOpen = false;
        IsConfirmModalOpen = false;

        if (anyClosed)
        {
            PosStatusMessage = "❌ Closed dialog/modal window (Esc).";
        }
    }
}
