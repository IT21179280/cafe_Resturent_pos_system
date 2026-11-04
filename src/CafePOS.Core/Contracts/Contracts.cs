using System.Collections.Generic;
using CafePOS.Core.Enums;

namespace CafePOS.Core.Contracts;

// ---------- Auth / Users ----------
public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserDto User);
public record RefreshTokenRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UserDto(Guid Id, string Email, string UserName, string FullName, UserRole RoleName, bool IsActive,
    bool MustChangePassword, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);
public record CreateUserRequest(string Email, string FullName, UserRole Role, string Password);
public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive);
public record ResetPasswordRequest(string NewPassword);

// ---------- Catalog ----------
public record CategoryDto(Guid Id, string Name, string? Description, int DisplayOrder, bool IsActive, int ProductCount,
    string? ImageUrl);
public record SaveCategoryRequest(string Name, string? Description, int DisplayOrder, bool IsActive, string? ImageUrl);

public record ProductModifierDto(Guid Id, string Name, decimal PriceDelta, bool IsActive);
public record SaveProductModifierRequest(Guid? Id, string Name, decimal PriceDelta, bool IsActive);

public record ProductDto(Guid Id, string Name, string Sku, string? Barcode, Guid CategoryId, string CategoryName,
    string? Description, decimal Price, decimal CostPrice, string? ImageUrl, bool IsAvailable,
    PreparationStation PreparationStation, decimal TaxRate, bool TrackInventory, List<ProductModifierDto> Modifiers);
public record SaveProductRequest(string Name, string Sku, string? Barcode, Guid CategoryId, string? Description,
    decimal Price, decimal CostPrice, string? ImageUrl, bool IsAvailable, PreparationStation PreparationStation,
    decimal TaxRate, bool TrackInventory, List<SaveProductModifierRequest>? Modifiers);

public record ProductImportResult(int Imported, int Skipped, List<string> Errors);

public record TableDto(Guid Id, string Number, int Capacity, TableStatus Status, string? Area, int DisplayOrder, Guid? CurrentOrderId, string? CurrentOrderNumber);
public record SaveTableRequest(string Number, int Capacity, string? Area, int DisplayOrder);
public record SetTableStatusRequest(TableStatus Status);

// ---------- Orders ----------
public record OrderItemModifierRequest(string Name, decimal Price);
public record OrderItemRequest(Guid? ProductId, string ProductName, decimal UnitPrice, decimal Quantity,
    decimal DiscountAmount, decimal TaxRate, string? Notes, List<Guid>? ModifierIds, List<OrderItemModifierRequest>? Modifiers,
    PreparationStation PreparationStation = PreparationStation.Kitchen);

public record CreateOrderRequest(OrderType Type, Guid? TableId, string? CustomerName, string? CustomerPhone,
    string? DeliveryAddress, string? Notes, List<OrderItemRequest> Items, DiscountType DiscountType, decimal DiscountValue,
    Guid? DiscountId, decimal? ServiceChargeRate, string? ClientOrderId, bool SendToKitchen);

public record UpdateOrderRequest(OrderType Type, Guid? TableId, string? CustomerName, string? CustomerPhone,
    string? DeliveryAddress, string? Notes, List<OrderItemRequest> Items, DiscountType DiscountType, decimal DiscountValue,
    Guid? DiscountId, decimal? ServiceChargeRate, bool SendToKitchen);

public record OrderItemModifierDto(Guid Id, string Name, decimal Price);
public record OrderItemDto(Guid Id, Guid? ProductId, string ProductName, decimal UnitPrice, decimal Quantity,
    decimal DiscountAmount, decimal TaxRate, decimal TaxAmount, decimal LineTotal, string? Notes,
    PreparationStation PreparationStation, List<OrderItemModifierDto> Modifiers);

public record PaymentDto(Guid Id, PaymentMethod Method, decimal Amount, decimal ChangeGiven, string? Reference,
    PaymentStatus Status, decimal RefundedAmount, DateTimeOffset PaidAt);

public record RefundDto(Guid Id, decimal Amount, string Reason, DateTimeOffset RefundedAt);

public record OrderDto(Guid Id, string OrderNumber, OrderType Type, OrderStatus Status, Guid? TableId, string? TableNumber,
    Guid CashierId, string CashierName, Guid? ShiftId, string? CustomerName, string? CustomerPhone, string? DeliveryAddress,
    string? Notes, decimal Subtotal, DiscountType DiscountType, decimal DiscountValue, decimal DiscountAmount,
    decimal TaxAmount, decimal ServiceCharge, decimal Total, decimal PaidAmount, decimal ChangeDue,
    DateTimeOffset PlacedAt, DateTimeOffset? PreparingAt, DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt,
    DateTimeOffset? CompletedAt, string? CancelReason, List<OrderItemDto> Items, List<PaymentDto> Payments,
    List<RefundDto> Refunds);

public record OrderSummaryDto(Guid Id, string OrderNumber, OrderType Type, OrderStatus Status, string? TableNumber,
    string? CustomerName, decimal Total, decimal PaidAmount, DateTimeOffset PlacedAt, int ItemCount);

public record AddPaymentRequest(PaymentMethod Method, decimal Amount, decimal ChangeGiven, string? Reference);
public record CheckoutRequest(List<AddPaymentRequest> Payments, bool CompleteOrder);
public record RefundRequest(Guid PaymentId, decimal Amount, string Reason);
public record CancelOrderRequest(string Reason);
public record MoveOrderRequest(Guid? TableId);

// ---------- Inventory ----------
public record IngredientDto(Guid Id, string Name, string? Sku, string Unit, decimal CurrentStock,
    decimal LowStockThreshold, decimal CostPerUnit, Guid? DefaultSupplierId, string? DefaultSupplierName, bool IsLowStock);
public record SaveIngredientRequest(string Name, string? Sku, string Unit, decimal CurrentStock,
    decimal LowStockThreshold, decimal CostPerUnit, Guid? DefaultSupplierId);
public record StockAdjustmentRequest(decimal Quantity, string Reason);
public record RecipeItemDto(Guid Id, Guid IngredientId, string IngredientName, string Unit, decimal Quantity, decimal CostPerUnit);
public record SaveRecipeRequest(List<SaveRecipeItemRequest> Items);
public record SaveRecipeItemRequest(Guid IngredientId, decimal Quantity);
public record InventoryMovementDto(Guid Id, string IngredientName, InventoryMovementType Type, decimal Quantity,
    decimal UnitCost, string Reason, string? Reference, decimal BalanceAfter, DateTimeOffset OccurredAt, string? UserName);

public record SupplierDto(Guid Id, string Name, string? ContactPerson, string? Phone, string? Email, string? Address,
    string? Notes, decimal Balance, bool IsDeleted);
public record SaveSupplierRequest(string Name, string? ContactPerson, string? Phone, string? Email, string? Address, string? Notes);

public record PurchaseOrderItemDto(Guid Id, Guid IngredientId, string IngredientName, string Unit, decimal Quantity,
    decimal UnitCost, decimal ReceivedQuantity, decimal LineTotal);
public record PurchaseOrderDto(Guid Id, string Number, Guid SupplierId, string SupplierName, PurchaseOrderStatus Status,
    DateTimeOffset OrderedAt, DateTimeOffset? ReceivedAt, string? Notes, decimal Total, List<PurchaseOrderItemDto> Items,
    string CreatedByName);
public record CreatePurchaseOrderRequest(Guid SupplierId, string? Notes, List<CreatePurchaseOrderItemRequest> Items);
public record CreatePurchaseOrderItemRequest(Guid IngredientId, decimal Quantity, decimal UnitCost);
public record ReceivePurchaseOrderRequest(List<ReceivePurchaseOrderItemRequest> Items);
public record ReceivePurchaseOrderItemRequest(Guid ItemId, decimal Quantity);

// ---------- Shifts ----------
public record ShiftDto(Guid Id, Guid UserId, string UserName, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt,
    decimal OpeningCash, decimal ExpectedCash, decimal CountedCash, decimal Difference, ShiftStatus Status,
    string? Notes, decimal CashSales, decimal CardSales, decimal BankSales, decimal CashRefunds, decimal Expenses);
public record OpenShiftRequest(decimal OpeningCash);
public record CloseShiftRequest(decimal CountedCash, string? Notes);
public record ShiftExpenseRequest(string Description, decimal Amount, string? Category);

// ---------- Dashboard / Reports ----------
public record DashboardDto(decimal TodaySales, int TodayOrders, decimal AverageOrderValue, int OpenTables,
    int PendingKitchenOrders, List<TopProductDto> TopProducts, List<LowStockDto> LowStockItems,
    List<OrderSummaryDto> RecentOrders, List<HourlySalesDto> HourlySales);

public record TopProductDto(Guid? ProductId, string Name, decimal Quantity, decimal Revenue);
public record LowStockDto(Guid Id, string Name, string Unit, decimal CurrentStock, decimal LowStockThreshold);
public record HourlySalesDto(int Hour, decimal Sales, int Orders);

public record SalesByDayDto(DateOnly Date, int Orders, decimal GrossSales, decimal Discounts, decimal Tax, decimal NetSales);
public record SalesByCashierDto(string Cashier, int Orders, decimal Sales);
public record SalesByPaymentDto(string Method, int Count, decimal Amount);
public record SalesByCategoryDto(string Category, decimal Quantity, decimal Sales);
public record BestSellerDto(string Product, decimal Quantity, decimal Revenue);
public record WastageDto(DateTimeOffset Date, string Ingredient, decimal Quantity, string Unit, string Reason, decimal Value);
public record RefundReportDto(string OrderNumber, decimal Amount, string Reason, string ApprovedBy, DateTimeOffset Date);
public record CancellationReportDto(string OrderNumber, string Reason, decimal Total, string Cashier, DateTimeOffset Date);
public record ShiftSummaryDto(string Cashier, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, decimal OpeningCash,
    decimal CashSales, decimal ExpectedCash, decimal CountedCash, decimal Difference, string Status);
public record GrossProfitDto(DateOnly Date, decimal Revenue, decimal Cost, decimal Profit, decimal MarginPercent);
public record InventoryMovementReportDto(DateTimeOffset Date, string Ingredient, string Type, decimal Quantity,
    string Unit, decimal Value, string Reason, string? User);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public record ApiErrorResponse(string Message, string? Detail = null, Dictionary<string, string[]>? Errors = null);

public record AppSettingsDto(string RestaurantName, string RestaurantAddress, string RestaurantPhone,
    string RestaurantEmail, string Currency, decimal ServiceChargeRate, decimal DefaultTaxRate,
    string ReceiptFooter, string InventoryDeductionEvent, string SystemName, string? LogoUrl,
    string? FirebaseDatabaseUrl, bool FirebaseSyncEnabled, bool FirebaseHasAuthToken,
    DateTimeOffset? FirebaseLastSyncedAt, string? FirebaseLastError);
public record UpdateAppSettingsRequest(string RestaurantName, string RestaurantAddress, string RestaurantPhone,
    string RestaurantEmail, decimal ServiceChargeRate, decimal DefaultTaxRate, string ReceiptFooter,
    string InventoryDeductionEvent, string SystemName, string? LogoUrl,
    string? FirebaseDatabaseUrl, bool FirebaseSyncEnabled, string? FirebaseAuthToken, bool ClearFirebaseAuthToken);

public record BrandingDto(string SystemName, string? LogoUrl, string RestaurantName);
public record ImageUploadResult(string Url);
public record FirebaseSyncResult(bool Success, string? Message, DateTimeOffset AttemptedAt);
