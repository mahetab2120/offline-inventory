using Domain.Enums;

namespace Domain.Entities;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BusinessCode { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string GSTIN { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; } = BusinessType.Retail;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string FinancialYearStart { get; set; } = "2026-04-01";
    public string InvoicePrefix { get; set; } = "INV-";
    public long NextInvoiceNumber { get; set; } = 1;
    public string CurrencySymbol { get; set; } = "₹";
    public bool IsInitialized { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? InitializedAtUtc { get; set; }
}
