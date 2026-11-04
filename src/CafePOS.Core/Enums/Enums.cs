namespace CafePOS.Core.Enums;

public enum UserRole
{
    Administrator = 0,
    Manager = 1,
    Cashier = 2,
    Kitchen = 3
}

public enum OrderType
{
    DineIn = 0,
    Takeaway = 1,
    Delivery = 2
}

public enum OrderStatus
{
    Draft = 0,
    Pending = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4,
    Completed = 5,
    Cancelled = 6
}

public enum TableStatus
{
    Available = 0,
    Occupied = 1,
    Reserved = 2
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    BankTransfer = 2
}

public enum PaymentStatus
{
    Completed = 0,
    Refunded = 1,
    PartiallyRefunded = 2
}

public enum InventoryMovementType
{
    Opening = 0,
    PurchaseReceipt = 1,
    Sale = 2,
    Refund = 3,
    Adjustment = 4,
    Wastage = 5,
    Cancellation = 6
}

public enum ShiftStatus
{
    Open = 0,
    Closed = 1
}

public enum PreparationStation
{
    None = 0,
    Kitchen = 1,
    Bar = 2,
    Bakery = 3,
    Grill = 4,
    Dessert = 5
}

public enum DiscountType
{
    None = 0,
    Percentage = 1,
    FixedAmount = 2
}

public enum PurchaseOrderStatus
{
    Draft = 0,
    Ordered = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4
}
