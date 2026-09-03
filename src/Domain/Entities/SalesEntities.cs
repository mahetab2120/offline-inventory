using Domain.Enums;

namespace Domain.Entities;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? GSTIN { get; set; }
    public string? Address { get; set; }
    public decimal OutstandingBalance { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? GSTIN { get; set; }
    public string? Address { get; set; }
    public decimal OutstandingBalance { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerGSTIN { get; set; }
    
    public decimal SubTotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TotalCGST { get; set; }
    public decimal TotalSGST { get; set; }
    public decimal TotalIGST { get; set; }
    public decimal TotalCess { get; set; }
    public decimal TotalTax => TotalCGST + TotalSGST + TotalIGST + TotalCess;
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal AmountPaid { get; set; }
    public decimal ChangeDue { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Finalized;
    public Guid CashierUserId { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<InvoiceItem> Items { get; set; } = new();
}

public class InvoiceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string HSNCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "PCS";
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GSTRate { get; set; }
    public decimal CGSTAmount { get; set; }
    public decimal SGSTAmount { get; set; }
    public decimal IGSTAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
