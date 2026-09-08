namespace Domain.Entities;

public class DecryptedAuditPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BusinessCode { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? GSTIN { get; set; }
    public string AccountingPeriod { get; set; } = string.Empty;
    public DateTime ExportTimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime DecryptedAtUtc { get; set; } = DateTime.UtcNow;
    public int TotalInvoices { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TaxableTurnover { get; set; }
    public decimal TotalCGST { get; set; }
    public decimal TotalSGST { get; set; }
    public decimal TotalIGST { get; set; }
    public decimal TotalTax { get; set; }
    public int TotalProducts { get; set; }
    public decimal InventoryValuation { get; set; }
    public int AuditLogsCount { get; set; }
    public string? PackageFilePath { get; set; }
    public string? RawPayloadJson { get; set; }
}
