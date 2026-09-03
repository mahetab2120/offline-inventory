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

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string HSNCode { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = "General";
    public string Unit { get; set; } = "PCS";
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal MRP { get; set; }
    public decimal GSTRate { get; set; } = 18.00m;
    public decimal CessRate { get; set; } = 0.00m;
    public decimal CurrentStock { get; set; } = 0;
    public decimal MinStockAlert { get; set; } = 5;
    public Guid? RackLocationId { get; set; }
    public string? RackLocation { get; set; } = "Shelf A-01";
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
