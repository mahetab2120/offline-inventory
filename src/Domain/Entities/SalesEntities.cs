using System.ComponentModel;
using System.Runtime.CompilerServices;
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

public class Invoice : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private Guid _id = Guid.NewGuid();
    public Guid Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    private string _invoiceNumber = string.Empty;
    public string InvoiceNumber { get => _invoiceNumber; set { _invoiceNumber = value; OnPropertyChanged(); } }

    private DateTime _invoiceDateUtc = DateTime.UtcNow;
    public DateTime InvoiceDateUtc { get => _invoiceDateUtc; set { _invoiceDateUtc = value; OnPropertyChanged(); } }

    private Guid? _customerId;
    public Guid? CustomerId { get => _customerId; set { _customerId = value; OnPropertyChanged(); } }

    private string _customerName = "Walk-in Customer";
    public string CustomerName { get => _customerName; set { _customerName = value; OnPropertyChanged(); } }

    private string _customerPhone = string.Empty;
    public string CustomerPhone { get => _customerPhone; set { _customerPhone = value; OnPropertyChanged(); } }

    private string? _customerGSTIN;
    public string? CustomerGSTIN { get => _customerGSTIN; set { _customerGSTIN = value; OnPropertyChanged(); } }
    
    private decimal _subTotal;
    public decimal SubTotal { get => _subTotal; set { _subTotal = value; OnPropertyChanged(); } }

    private decimal _totalDiscount;
    public decimal TotalDiscount { get => _totalDiscount; set { _totalDiscount = value; OnPropertyChanged(); } }

    private decimal _taxableAmount;
    public decimal TaxableAmount { get => _taxableAmount; set { _taxableAmount = value; OnPropertyChanged(); } }

    private decimal _totalCGST;
    public decimal TotalCGST { get => _totalCGST; set { _totalCGST = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalTax)); } }

    private decimal _totalSGST;
    public decimal TotalSGST { get => _totalSGST; set { _totalSGST = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalTax)); } }

    private decimal _totalIGST;
    public decimal TotalIGST { get => _totalIGST; set { _totalIGST = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalTax)); } }

    private decimal _totalCess;
    public decimal TotalCess { get => _totalCess; set { _totalCess = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalTax)); } }

    public decimal TotalTax => TotalCGST + TotalSGST + TotalIGST + TotalCess;

    private decimal _roundOff;
    public decimal RoundOff { get => _roundOff; set { _roundOff = value; OnPropertyChanged(); } }

    private decimal _grandTotal;
    public decimal GrandTotal { get => _grandTotal; set { _grandTotal = value; OnPropertyChanged(); } }
    
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    public PaymentMethod PaymentMethod { get => _paymentMethod; set { _paymentMethod = value; OnPropertyChanged(); } }

    private decimal _amountPaid;
    public decimal AmountPaid { get => _amountPaid; set { _amountPaid = value; OnPropertyChanged(); } }

    private decimal _changeDue;
    public decimal ChangeDue { get => _changeDue; set { _changeDue = value; OnPropertyChanged(); } }

    private InvoiceStatus _status = InvoiceStatus.Finalized;
    public InvoiceStatus Status { get => _status; set { _status = value; OnPropertyChanged(); } }

    private Guid _cashierUserId;
    public Guid CashierUserId { get => _cashierUserId; set { _cashierUserId = value; OnPropertyChanged(); } }

    private string _cashierUsername = string.Empty;
    public string CashierUsername { get => _cashierUsername; set { _cashierUsername = value; OnPropertyChanged(); } }

    private string? _notes;
    public string? Notes { get => _notes; set { _notes = value; OnPropertyChanged(); } }

    public List<InvoiceItem> Items { get; set; } = new();
}

public class InvoiceItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private Guid _id = Guid.NewGuid();
    public Guid Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    private Guid _invoiceId;
    public Guid InvoiceId { get => _invoiceId; set { _invoiceId = value; OnPropertyChanged(); } }

    private Guid _productId;
    public Guid ProductId { get => _productId; set { _productId = value; OnPropertyChanged(); } }

    private string _productName = string.Empty;
    public string ProductName { get => _productName; set { _productName = value; OnPropertyChanged(); } }

    private string _sku = string.Empty;
    public string SKU { get => _sku; set { _sku = value; OnPropertyChanged(); } }

    private string _hsnCode = string.Empty;
    public string HSNCode { get => _hsnCode; set { _hsnCode = value; OnPropertyChanged(); } }

    private decimal _quantity = 1;
    public decimal Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedQuantity)); } }

    private string _unit = "PCS";
    public string Unit { get => _unit; set { _unit = value; OnPropertyChanged(); } }

    private decimal _unitPrice;
    public decimal UnitPrice { get => _unitPrice; set { _unitPrice = value; OnPropertyChanged(); } }

    private decimal _discountAmount;
    public decimal DiscountAmount { get => _discountAmount; set { _discountAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiscount)); } }

    private decimal _taxableValue;
    public decimal TaxableValue { get => _taxableValue; set { _taxableValue = value; OnPropertyChanged(); } }

    private decimal _gstRate = 18;
    public decimal GSTRate { get => _gstRate; set { _gstRate = value; OnPropertyChanged(); } }

    private decimal _cgstAmount;
    public decimal CGSTAmount { get => _cgstAmount; set { _cgstAmount = value; OnPropertyChanged(); } }

    private decimal _sgstAmount;
    public decimal SGSTAmount { get => _sgstAmount; set { _sgstAmount = value; OnPropertyChanged(); } }

    private decimal _igstAmount;
    public decimal IGSTAmount { get => _igstAmount; set { _igstAmount = value; OnPropertyChanged(); } }

    private decimal _totalAmount;
    public decimal TotalAmount { get => _totalAmount; set { _totalAmount = value; OnPropertyChanged(); } }

    public string FormattedQuantity => $"x{Quantity:0.##}";
    public bool HasDiscount => DiscountAmount > 0;
}
