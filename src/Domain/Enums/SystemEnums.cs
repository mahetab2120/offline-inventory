namespace Domain.Enums;

public enum BusinessType
{
    Retail = 1,
    Wholesale = 2,
    Supermarket = 3,
    MegaMall = 4,
    Pharmacy = 5,
    Manufacturing = 6,
    GeneralStore = 7,
    Other = 99
}

public enum SubscriptionTier
{
    Basic = 1,
    Professional = 2,
    Premium = 3,
    Enterprise = 4,
    PharmacySpecial = 5,
    Custom = 99
}

public enum UserRole
{
    SuperAdmin_CA = 1,
    BusinessAdmin = 2,
    StoreManager = 3,
    InventoryManager = 4,
    Cashier = 5,
    Auditor = 6
}

public enum LicenseStatus
{
    Uninitialized = 0,
    Active = 1,
    ExpiringSoon = 2,
    InGracePeriod = 3,
    Expired = 4,
    Suspended = 5
}

[Flags]
public enum SystemPermissions : long
{
    None = 0,
    ViewDashboard = 1 << 0,
    CreateInvoice = 1 << 1,
    EditInvoice = 1 << 2,
    CancelInvoice = 1 << 3,
    PrintInvoice = 1 << 4,
    ApplyDiscount = 1 << 5,
    ManageInventory = 1 << 6,
    ViewCostPrice = 1 << 7,
    ViewSellingPrice = 1 << 8,
    ManageRacks = 1 << 9,
    ManagePurchases = 1 << 10,
    ManageSuppliers = 1 << 11,
    ManageCustomers = 1 << 12,
    ViewReports = 1 << 13,
    ExportCAData = 1 << 14,
    ManageUsers = 1 << 15,
    ManageSettings = 1 << 16,
    ManageLicense = 1 << 17,
    BackupRestore = 1 << 18,
    All = -1
}

public enum AuditActionType
{
    Login = 1,
    Logout = 2,
    DataKeyUploaded = 3,
    LicenseRenewed = 4,
    ProductCreated = 5,
    ProductUpdated = 6,
    StockAdjusted = 7,
    InvoiceCreated = 8,
    InvoiceCancelled = 9,
    InvoiceReprinted = 10,
    PurchaseCreated = 11,
    UserCreated = 12,
    UserUpdated = 13,
    SettingsChanged = 14,
    DataExported = 15,
    BackupCreated = 16,
    DatabaseRestored = 17
}

public enum InvoiceStatus
{
    Draft = 1,
    Finalized = 2,
    Cancelled = 3,
    Returned = 4
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    UPI = 3,
    Credit = 4,
    Split = 5
}
