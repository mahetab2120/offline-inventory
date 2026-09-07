using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Domain.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class RackLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Warehouse { get; set; } = "Main Warehouse";
    public string Section { get; set; } = "A";
    public string Rack { get; set; } = "R01";
    public string Shelf { get; set; } = "S01";
    public string Bin { get; set; } = "B01";
    public string FullLocationPath => $"{Warehouse} → Section {Section} → Rack {Rack} → Shelf {Shelf} → Bin {Bin}";
}

public class Product : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private Guid _id = Guid.NewGuid();
    public Guid Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    private string _name = string.Empty;
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    private string _sku = string.Empty;
    public string SKU { get => _sku; set { _sku = value; OnPropertyChanged(); } }

    private string _barcode = string.Empty;
    public string Barcode { get => _barcode; set { _barcode = value; OnPropertyChanged(); } }

    private string _hsnCode = string.Empty;
    public string HSNCode { get => _hsnCode; set { _hsnCode = value; OnPropertyChanged(); } }

    private Guid? _categoryId;
    public Guid? CategoryId { get => _categoryId; set { _categoryId = value; OnPropertyChanged(); } }

    private string _categoryName = "General";
    public string CategoryName { get => _categoryName; set { _categoryName = value; OnPropertyChanged(); } }

    private string _unit = "PCS";
    public string Unit { get => _unit; set { _unit = value; OnPropertyChanged(); } }

    private decimal _purchasePrice;
    public decimal PurchasePrice { get => _purchasePrice; set { _purchasePrice = value; OnPropertyChanged(); } }

    private decimal _sellingPrice;
    public decimal SellingPrice { get => _sellingPrice; set { _sellingPrice = value; OnPropertyChanged(); } }

    private decimal _mrp;
    public decimal MRP { get => _mrp; set { _mrp = value; OnPropertyChanged(); } }

    private decimal _gstRate = 18.00m;
    public decimal GSTRate { get => _gstRate; set { _gstRate = value; OnPropertyChanged(); } }

    private decimal _cessRate = 0.00m;
    public decimal CessRate { get => _cessRate; set { _cessRate = value; OnPropertyChanged(); } }

    private decimal _currentStock = 0;
    public decimal CurrentStock { get => _currentStock; set { _currentStock = value; OnPropertyChanged(); } }

    private decimal _minStockAlert = 5;
    public decimal MinStockAlert { get => _minStockAlert; set { _minStockAlert = value; OnPropertyChanged(); } }

    private Guid? _rackLocationId;
    public Guid? RackLocationId { get => _rackLocationId; set { _rackLocationId = value; OnPropertyChanged(); } }

    private string? _rackLocation = "Shelf A-01";
    public string? RackLocation { get => _rackLocation; set { _rackLocation = value; OnPropertyChanged(); } }

    private string? _batchNumber;
    public string? BatchNumber { get => _batchNumber; set { _batchNumber = value; OnPropertyChanged(); } }

    private DateTime? _expiryDate;
    public DateTime? ExpiryDate { get => _expiryDate; set { _expiryDate = value; OnPropertyChanged(); } }

    private bool _isActive = true;
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

    private DateTime _createdAtUtc = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get => _createdAtUtc; set { _createdAtUtc = value; OnPropertyChanged(); } }
}
