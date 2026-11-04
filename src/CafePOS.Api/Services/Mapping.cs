using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;

namespace CafePOS.Api.Services;

public static class Mapping
{
    public static OrderItemDto ToDto(OrderItem item) => new(
        item.Id, item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.DiscountAmount,
        item.TaxRate, item.TaxAmount, item.LineTotal, item.Notes, item.PreparationStation,
        item.Modifiers.Select(m => new OrderItemModifierDto(m.Id, m.Name, m.Price)).ToList());

    public static PaymentDto ToDto(Payment payment) => new(
        payment.Id, payment.Method, payment.Amount, payment.ChangeGiven, payment.Reference, payment.Status,
        payment.RefundedAmount, payment.PaidAt);

    public static OrderDto ToDto(Order order) => new(
        order.Id, order.OrderNumber, order.Type, order.Status, order.TableId, order.Table?.Number,
        order.CashierId, order.Cashier?.FullName ?? order.Cashier?.UserName ?? string.Empty, order.ShiftId,
        order.CustomerName, order.CustomerPhone, order.DeliveryAddress, order.Notes,
        order.Subtotal, order.DiscountType, order.DiscountValue, order.DiscountAmount,
        order.TaxAmount, order.ServiceCharge, order.Total, order.PaidAmount, order.ChangeDue,
        order.PlacedAt, order.PreparingAt, order.ReadyAt, order.ServedAt, order.CompletedAt, order.CancelReason,
        order.Items.OrderBy(i => i.CreatedAt).Select(ToDto).ToList(),
        order.Payments.OrderBy(p => p.PaidAt).Select(ToDto).ToList(),
        order.Refunds.OrderBy(r => r.RefundedAt).Select(r => new RefundDto(r.Id, r.Amount, r.Reason, r.RefundedAt)).ToList());

    public static OrderSummaryDto ToSummary(Order order) => new(
        order.Id, order.OrderNumber, order.Type, order.Status, order.Table?.Number, order.CustomerName,
        order.Total, order.PaidAmount, order.PlacedAt, order.Items.Count);

    public static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Sku, p.Barcode, p.CategoryId, p.Category?.Name ?? string.Empty, p.Description,
        p.Price, p.CostPrice, p.ImageUrl, p.IsAvailable, p.PreparationStation, p.TaxRate, p.TrackInventory,
        p.Modifiers.Where(m => !m.IsDeleted).Select(m => new ProductModifierDto(m.Id, m.Name, m.PriceDelta, m.IsActive)).ToList());

    public static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Description, c.DisplayOrder, c.IsActive,
        c.Products.Count(p => !p.IsDeleted), c.ImageUrl);

    public static TableDto ToDto(DiningTable t, string? orderNumber = null) => new(
        t.Id, t.Number, t.Capacity, t.Status, t.Area, t.DisplayOrder, t.CurrentOrderId, orderNumber);

    public static IngredientDto ToDto(Ingredient i) => new(
        i.Id, i.Name, i.Sku, i.Unit, i.CurrentStock, i.LowStockThreshold, i.CostPerUnit,
        i.DefaultSupplierId, i.DefaultSupplier?.Name, i.CurrentStock <= i.LowStockThreshold);
}
