using Domain.Enums;

namespace Domain.Entities;

public class LicenseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BusinessCode { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public SubscriptionTier Plan { get; set; } = SubscriptionTier.Basic;
    public DateTime IssuedDateUtc { get; set; }
    public DateTime ExpiryDateUtc { get; set; }
    public int GracePeriodDays { get; set; } = 7;
    public int MaxUsers { get; set; } = 5;
    public int MaxBranches { get; set; } = 1;
    public int MaxProducts { get; set; } = 10000;
    public string EnabledModulesJson { get; set; } = "[\"Billing\",\"Inventory\",\"GSTReports\"]";
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;
    public string Signature { get; set; } = string.Empty;
    public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;
}
