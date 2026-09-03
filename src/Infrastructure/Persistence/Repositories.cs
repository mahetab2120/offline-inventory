using System.Collections.Concurrent;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Persistence;

public class InMemoryDataStore
{
    public static readonly InMemoryDataStore Instance = new();

    public Company? Company { get; set; }
    public ConcurrentDictionary<string, User> UsersByUsername { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<Guid, User> UsersById { get; } = new();
    public LicenseRecord? CurrentLicense { get; set; }
    public ConcurrentBag<AuditLog> AuditLogs { get; } = new();
    public ConcurrentDictionary<Guid, Product> Products { get; } = new();
    public ConcurrentDictionary<Guid, Invoice> Invoices { get; } = new();
    private long _auditCounter = 1;
    private long _invoiceCounter = 1;

    public long NextAuditId => Interlocked.Increment(ref _auditCounter);
    public long NextInvoiceNumber => Interlocked.Increment(ref _invoiceCounter);
}

public class LocalCompanyRepository : ICompanyRepository
{
    private readonly InMemoryDataStore _store = InMemoryDataStore.Instance;

    public Task<Company?> GetCompanyAsync()
    {
        return Task.FromResult(_store.Company);
    }

    public Task<bool> SaveCompanyAsync(Company company)
    {
        _store.Company = company;
        return Task.FromResult(true);
    }

    public Task<bool> IsCompanyInitializedAsync()
    {
        return Task.FromResult(_store.Company != null && _store.Company.IsInitialized);
    }
}

public class LocalUserRepository : IUserRepository
{
    private readonly InMemoryDataStore _store = InMemoryDataStore.Instance;

    public Task<User?> GetByUsernameAsync(string username)
    {
        _store.UsersByUsername.TryGetValue(username, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(Guid id)
    {
        _store.UsersById.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_store.UsersById.Values.ToList());
    }

    public Task<bool> CreateUserAsync(User user)
    {
        _store.UsersByUsername[user.Username] = user;
        _store.UsersById[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateUserAsync(User user)
    {
        _store.UsersByUsername[user.Username] = user;
        _store.UsersById[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateLastLoginAsync(Guid userId)
    {
        if (_store.UsersById.TryGetValue(userId, out var user))
        {
            user.LastLoginAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

public class LocalLicenseRepository : ILicenseRepository
{
    private readonly InMemoryDataStore _store = InMemoryDataStore.Instance;

    public Task<LicenseRecord?> GetCurrentLicenseAsync()
    {
        return Task.FromResult(_store.CurrentLicense);
    }

    public Task<bool> SaveLicenseAsync(LicenseRecord license)
    {
        _store.CurrentLicense = license;
        return Task.FromResult(true);
    }
}

public class LocalAuditLogRepository : IAuditLogRepository
{
    private readonly InMemoryDataStore _store = InMemoryDataStore.Instance;

    public Task LogAsync(AuditLog log)
    {
        log.Id = _store.NextAuditId;
        _store.AuditLogs.Add(log);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 100)
    {
        var logs = _store.AuditLogs.OrderByDescending(l => l.TimestampUtc).Take(count).ToList();
        return Task.FromResult<IEnumerable<AuditLog>>(logs);
    }
}

public class LocalProductRepository : IProductRepository
{
    private readonly InMemoryDataStore _store = InMemoryDataStore.Instance;

    public Task<Product?> GetByIdAsync(Guid id)
    {
        _store.Products.TryGetValue(id, out var p);
        return Task.FromResult(p);
    }

    public Task<Product?> GetByBarcodeOrSkuAsync(string code)
    {
        var p = _store.Products.Values.FirstOrDefault(x => 
            x.Barcode.Equals(code, StringComparison.OrdinalIgnoreCase) || 
            x.SKU.Equals(code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(p);
    }

    public Task<IEnumerable<Product>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Product>>(_store.Products.Values.ToList());
    }

    public Task<IEnumerable<Product>> GetLowStockProductsAsync()
    {
        var low = _store.Products.Values.Where(p => p.CurrentStock <= p.MinStockAlert && p.IsActive).ToList();
        return Task.FromResult<IEnumerable<Product>>(low);
    }

    public Task<bool> CreateAsync(Product product)
    {
        _store.Products[product.Id] = product;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateAsync(Product product)
    {
        _store.Products[product.Id] = product;
        return Task.FromResult(true);
    }

    public Task<bool> AdjustStockAsync(Guid productId, decimal quantityChange)
    {
        if (_store.Products.TryGetValue(productId, out var p))
        {
            p.CurrentStock += quantityChange;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

public class LocalInvoiceRepository : IInvoiceRepository
{
    private readonly InMemoryDataStore _store = InMemoryDataStore.Instance;

    public Task<Invoice?> GetByIdAsync(Guid id)
    {
        _store.Invoices.TryGetValue(id, out var inv);
        return Task.FromResult(inv);
    }

    public Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        var inv = _store.Invoices.Values.FirstOrDefault(i => i.InvoiceNumber.Equals(invoiceNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(inv);
    }

    public Task<IEnumerable<Invoice>> GetRecentInvoicesAsync(int limit = 50)
    {
        var list = _store.Invoices.Values.OrderByDescending(i => i.InvoiceDateUtc).Take(limit).ToList();
        return Task.FromResult<IEnumerable<Invoice>>(list);
    }

    public Task<string> GetNextInvoiceNumberAsync(string prefix)
    {
        long num = _store.NextInvoiceNumber;
        return Task.FromResult($"{prefix}{num:D6}");
    }

    public Task<bool> SaveInvoiceAtomicAsync(Invoice invoice)
    {
        _store.Invoices[invoice.Id] = invoice;
        return Task.FromResult(true);
    }
}
