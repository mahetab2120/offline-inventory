using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Models;
using Security.Interfaces;

namespace Application.Services;

public interface IBootstrapService
{
    Task<(bool Success, string Message)> BootstrapFromDataKeyAsync(ClientProvisioningPayload payload);
}

public class BootstrapService : IBootstrapService
{
    private readonly ICompanyRepository _companyRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILicenseRepository _licenseRepo;
    private readonly IAuditLogRepository _auditRepo;
    private readonly IPasswordHasher _passwordHasher;

    public BootstrapService(
        ICompanyRepository companyRepo,
        IUserRepository userRepo,
        ILicenseRepository licenseRepo,
        IAuditLogRepository auditRepo,
        IPasswordHasher passwordHasher)
    {
        _companyRepo = companyRepo;
        _userRepo = userRepo;
        _licenseRepo = licenseRepo;
        _auditRepo = auditRepo;
        _passwordHasher = passwordHasher;
    }

    public async Task<(bool Success, string Message)> BootstrapFromDataKeyAsync(ClientProvisioningPayload payload)
    {
        if (payload == null)
            return (false, "Invalid provisioning payload.");

        try
        {
            // 1. Setup Company Entity
            var company = new Company
            {
                Id = Guid.NewGuid(),
                BusinessCode = payload.BusinessCode,
                LegalName = payload.LegalName,
                TradeName = payload.TradeName,
                GSTIN = payload.GSTIN,
                PAN = payload.PAN,
                BusinessType = payload.BusinessType,
                Address = payload.Address,
                City = payload.City,
                State = payload.State,
                Pincode = payload.Pincode,
                ContactPhone = payload.ContactPhone,
                ContactEmail = payload.ContactEmail,
                FinancialYearStart = payload.FinancialYearStart,
                InvoicePrefix = string.IsNullOrWhiteSpace(payload.InvoicePrefix) ? "INV-" : payload.InvoicePrefix,
                IsInitialized = true,
                CreatedAtUtc = DateTime.UtcNow,
                InitializedAtUtc = DateTime.UtcNow
            };

            await _companyRepo.SaveCompanyAsync(company);

            // 2. Create Initial Business Admin Account
            string hash = payload.AdminPasswordHash;
            string salt = payload.AdminSalt;

            if (string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(payload.AdminTempPasswordPlain))
            {
                var (h, s) = _passwordHasher.HashPassword(payload.AdminTempPasswordPlain);
                hash = h;
                salt = s;
            }

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = payload.AdminUsername,
                FullName = payload.AdminFullName,
                PasswordHash = hash,
                Salt = salt,
                Role = UserRole.BusinessAdmin,
                Permissions = SystemPermissions.All,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _userRepo.CreateUserAsync(adminUser);

            // 3. Register Initial License Record
            var license = new LicenseRecord
            {
                Id = Guid.NewGuid(),
                BusinessCode = payload.BusinessCode,
                LicenseKey = $"INIT-{payload.BusinessCode}-{DateTime.UtcNow:yyyyMMdd}",
                Plan = payload.SubscriptionPlan,
                IssuedDateUtc = payload.IssuedDateUtc,
                ExpiryDateUtc = payload.ExpiryDateUtc,
                GracePeriodDays = payload.GracePeriodDays,
                MaxUsers = payload.MaxUsers,
                MaxBranches = payload.MaxBranches,
                MaxProducts = payload.MaxProducts,
                EnabledModulesJson = System.Text.Json.JsonSerializer.Serialize(payload.EnabledModules),
                Status = LicenseStatus.Active,
                LastVerifiedUtc = DateTime.UtcNow
            };

            await _licenseRepo.SaveLicenseAsync(license);

            // 4. Record Immutable Audit Log
            await _auditRepo.LogAsync(new AuditLog
            {
                UserId = adminUser.Id,
                Username = adminUser.Username,
                Module = "SystemProvisioning",
                Action = AuditActionType.DataKeyUploaded,
                RecordId = payload.BusinessCode,
                NewValue = $"Business {payload.LegalName} ({payload.BusinessCode}) provisioned and activated.",
                Reason = "First-time Encrypted Data Key upload."
            });

            return (true, "Business provisioned and initialized successfully!");
        }
        catch (Exception ex)
        {
            return (false, $"Error provisioning business: {ex.Message}");
        }
    }
}
