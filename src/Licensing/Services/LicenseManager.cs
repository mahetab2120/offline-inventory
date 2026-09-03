using Domain.Entities;
using Domain.Enums;

namespace Licensing.Services;

public interface ILicenseManager
{
    LicenseStatus EvaluateLicenseStatus(LicenseRecord license, DateTime currentUtc);
    int CalculateDaysRemaining(LicenseRecord license, DateTime currentUtc);
    bool CanPerformBilling(LicenseRecord license, DateTime currentUtc);
}

public class LicenseManager : ILicenseManager
{
    public LicenseStatus EvaluateLicenseStatus(LicenseRecord license, DateTime currentUtc)
    {
        if (license == null || string.IsNullOrWhiteSpace(license.LicenseKey))
            return LicenseStatus.Uninitialized;

        if (license.Status == LicenseStatus.Suspended)
            return LicenseStatus.Suspended;

        if (currentUtc < license.IssuedDateUtc.AddMinutes(-5))
        {
            // System clock manipulation / future issue date anomaly
            return LicenseStatus.Suspended;
        }

        if (currentUtc <= license.ExpiryDateUtc)
        {
            var remaining = (license.ExpiryDateUtc - currentUtc).TotalDays;
            return remaining <= 7 ? LicenseStatus.ExpiringSoon : LicenseStatus.Active;
        }

        // Past expiry
        var graceExpiry = license.ExpiryDateUtc.AddDays(license.GracePeriodDays);
        if (currentUtc <= graceExpiry)
        {
            return LicenseStatus.InGracePeriod;
        }

        return LicenseStatus.Expired;
    }

    public int CalculateDaysRemaining(LicenseRecord license, DateTime currentUtc)
    {
        if (license == null) return 0;
        var diff = (license.ExpiryDateUtc - currentUtc).TotalDays;
        return (int)Math.Max(0, Math.Ceiling(diff));
    }

    public bool CanPerformBilling(LicenseRecord license, DateTime currentUtc)
    {
        var status = EvaluateLicenseStatus(license, currentUtc);
        return status == LicenseStatus.Active || status == LicenseStatus.ExpiringSoon || status == LicenseStatus.InGracePeriod;
    }
}
