using Domain.Entities;
using Domain.Enums;
using Domain.Models;

namespace Application.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetCompanyAsync();
    Task<bool> SaveCompanyAsync(Company company);
    Task<bool> IsCompanyInitializedAsync();
}

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(Guid id);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<bool> CreateUserAsync(User user);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> UpdateLastLoginAsync(Guid userId);
}

public interface ILicenseRepository
{
    Task<LicenseRecord?> GetCurrentLicenseAsync();
    Task<bool> SaveLicenseAsync(LicenseRecord license);
}

public interface IAuditLogRepository
{
    Task LogAsync(AuditLog log);
    Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 100);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product?> GetByBarcodeOrSkuAsync(string code);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> GetLowStockProductsAsync();
    Task<bool> CreateAsync(Product product);
    Task<bool> UpdateAsync(Product product);
    Task<bool> AdjustStockAsync(Guid productId, decimal quantityChange);
}

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber);
    Task<IEnumerable<Invoice>> GetRecentInvoicesAsync(int limit = 50);
    Task<string> GetNextInvoiceNumberAsync(string prefix);
    Task<bool> SaveInvoiceAtomicAsync(Invoice invoice);
}
