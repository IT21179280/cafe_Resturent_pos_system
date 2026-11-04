using CafePOS.Core.Enums;

namespace CafePOS.Core.Entities;

public class Ingredient : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string Unit { get; set; } = "g";
    public decimal CurrentStock { get; set; }
    public decimal LowStockThreshold { get; set; }
    public decimal CostPerUnit { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public Supplier? DefaultSupplier { get; set; }
    public ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

public class RecipeItem : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }
}

public class InventoryMovement : BaseEntity
{
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? UserId { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public decimal Balance { get; set; }
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}

public class PurchaseOrder : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTimeOffset OrderedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? Notes { get; set; }
    public decimal Total { get; set; }
    public Guid CreatedByUserId { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class Shift : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Difference { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public string? Notes { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public ICollection<ShiftExpense> Expenses { get; set; } = new List<ShiftExpense>();
}

public class ShiftExpense : BaseEntity
{
    public Guid ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public Guid UserId { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = "system";
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class NumberSequence
{
    public string Name { get; set; } = string.Empty;
    public long Value { get; set; }
}

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SettingKeys
{
    public const string SystemName = "Branding.SystemName";
    public const string LogoUrl = "Branding.LogoUrl";
    public const string RestaurantName = "Restaurant.Name";
    public const string RestaurantAddress = "Restaurant.Address";
    public const string RestaurantPhone = "Restaurant.Phone";
    public const string RestaurantEmail = "Restaurant.Email";
    public const string Currency = "Currency.Code";
    public const string ServiceChargeRate = "Billing.ServiceChargeRate";
    public const string DefaultTaxRate = "Billing.DefaultTaxRate";
    public const string ReceiptFooter = "Receipt.Footer";
    public const string InventoryDeductionEvent = "Inventory.DeductionEvent";
    public const string LowStockAlerts = "Inventory.LowStockAlerts";
    public const string FirebaseDatabaseUrl = "Firebase.DatabaseUrl";
    public const string FirebaseAuthToken = "Firebase.AuthToken";
    public const string FirebaseSyncEnabled = "Firebase.SyncEnabled";
    public const string FirebaseLastSyncedAt = "Firebase.LastSyncedAt";
    public const string FirebaseLastError = "Firebase.LastError";
}
