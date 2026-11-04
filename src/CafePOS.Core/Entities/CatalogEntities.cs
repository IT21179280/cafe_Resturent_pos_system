using CafePOS.Core.Enums;

namespace CafePOS.Core.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public PreparationStation PreparationStation { get; set; } = PreparationStation.Kitchen;
    public decimal TaxRate { get; set; }
    public bool TrackInventory { get; set; }
    public ICollection<ProductModifier> Modifiers { get; set; } = new List<ProductModifier>();
    public ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();
}

public class ProductModifier : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Discount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public bool RequiresManagerApproval { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DiningTable : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;
    public string? Area { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? CurrentOrderId { get; set; }
}
