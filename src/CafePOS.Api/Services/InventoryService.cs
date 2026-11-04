using CafePOS.Api.Data;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

public class InventoryService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(AppDbContext db, AuditService audit, ILogger<InventoryService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Apply a signed stock change atomically and record an auditable movement.</summary>
    public async Task<InventoryMovement> ApplyAsync(Guid ingredientId, decimal quantityDelta, InventoryMovementType type,
        string reason, string? reference, Guid? userId, Guid? orderId = null, Guid? purchaseOrderId = null,
        decimal? unitCost = null, CancellationToken ct = default)
    {
        if (quantityDelta == 0)
            throw new InvalidOperationException("Stock change cannot be zero.");

        var ingredient = await _db.Ingredients.AsNoTracking().FirstOrDefaultAsync(i => i.Id == ingredientId, ct)
            ?? throw new KeyNotFoundException("Ingredient not found.");

        if (type is InventoryMovementType.PurchaseReceipt or InventoryMovementType.Opening)
            quantityDelta = Math.Abs(quantityDelta);

        var now = DateTimeOffset.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Ingredients"
            SET "CurrentStock" = "CurrentStock" + {quantityDelta}, "UpdatedAt" = {now}
            WHERE "Id" = {ingredientId} AND "IsDeleted" = false
            """, ct);

        var balance = await _db.Ingredients.AsNoTracking()
            .Where(i => i.Id == ingredientId).Select(i => i.CurrentStock).FirstAsync(ct);

        var movement = new InventoryMovement
        {
            IngredientId = ingredientId,
            Type = type,
            Quantity = quantityDelta,
            UnitCost = unitCost ?? ingredient.CostPerUnit,
            Reason = reason,
            Reference = reference,
            OrderId = orderId,
            PurchaseOrderId = purchaseOrderId,
            UserId = userId,
            BalanceAfter = balance,
            OccurredAt = now
        };
        _db.InventoryMovements.Add(movement);
        await _db.SaveChangesAsync(ct);

        if (balance <= ingredient.LowStockThreshold)
            _logger.LogWarning("Low stock: {Ingredient} is at {Balance} {Unit} (threshold {Threshold})",
                ingredient.Name, balance, ingredient.Unit, ingredient.LowStockThreshold);

        return movement;
    }

    /// <summary>Deducts recipe ingredients for an order exactly once (idempotent by order id).</summary>
    public async Task DeductForOrderAsync(Order order, Guid userId, CancellationToken ct = default)
    {
        var alreadyDeducted = await _db.InventoryMovements
            .AnyAsync(m => m.OrderId == order.Id && m.Type == InventoryMovementType.Sale, ct);
        if (alreadyDeducted) return;

        var items = order.Items.Where(i => i.ProductId.HasValue && i.Quantity > 0).ToList();
        if (items.Count == 0) return;

        var productIds = items.Select(i => i.ProductId!.Value).Distinct().ToList();
        var recipes = await _db.RecipeItems.AsNoTracking()
            .Where(r => productIds.Contains(r.ProductId))
            .Select(r => new { r.ProductId, r.IngredientId, r.Quantity, IngredientName = r.Ingredient!.Name, r.Ingredient!.Unit })
            .ToListAsync(ct);

        if (recipes.Count == 0) return;

        var recipeLookup = recipes.ToLookup(r => r.ProductId);
        foreach (var item in items)
        {
            foreach (var recipe in recipeLookup[item.ProductId!.Value])
            {
                var qty = recipe.Quantity * item.Quantity;
                if (qty <= 0) continue;
                await ApplyAsync(recipe.IngredientId, -qty, InventoryMovementType.Sale,
                    $"Sold via {order.OrderNumber}", order.OrderNumber, userId, orderId: order.Id, ct: ct);
            }
        }
    }

    /// <summary>Reverses the sale movements for a cancelled/served order exactly once.</summary>
    public async Task ReturnForOrderAsync(Order order, Guid userId, CancellationToken ct = default)
    {
        var saleMovements = await _db.InventoryMovements.AsNoTracking()
            .Where(m => m.OrderId == order.Id && m.Type == InventoryMovementType.Sale)
            .ToListAsync(ct);
        if (saleMovements.Count == 0) return;

        var alreadyReturned = await _db.InventoryMovements
            .AnyAsync(m => m.OrderId == order.Id && m.Type == InventoryMovementType.Cancellation, ct);
        if (alreadyReturned) return;

        foreach (var sale in saleMovements)
        {
            await ApplyAsync(sale.IngredientId, Math.Abs(sale.Quantity), InventoryMovementType.Cancellation,
                $"Cancelled {order.OrderNumber}", order.OrderNumber, userId, orderId: order.Id, ct: ct);
        }
    }

    public async Task AdjustAsync(Guid ingredientId, decimal delta, string reason, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required for stock adjustments.");
        var movement = await ApplyAsync(ingredientId, delta,
            delta >= 0 ? InventoryMovementType.Adjustment : InventoryMovementType.Wastage,
            reason, null, userId, ct: ct);
        await _audit.LogAsync("StockAdjustment", nameof(Ingredient), ingredientId.ToString(),
            new { movement.Quantity, movement.BalanceAfter, reason }, ct);
    }

    public async Task ReceivePurchaseOrderAsync(PurchaseOrder purchaseOrder, IReadOnlyDictionary<Guid, decimal> requested,
        Guid userId, CancellationToken ct = default)
    {
        foreach (var item in purchaseOrder.Items)
        {
            var remaining = item.Quantity - item.ReceivedQuantity;
            if (remaining <= 0) continue;

            var requestedQty = requested.TryGetValue(item.Id, out var value) ? value : remaining;
            if (requestedQty <= 0) continue;
            var receiveNow = Math.Min(requestedQty, remaining);

            item.ReceivedQuantity += receiveNow;
            await ApplyAsync(item.IngredientId, receiveNow, InventoryMovementType.PurchaseReceipt,
                $"Received {purchaseOrder.Number}", purchaseOrder.Number, userId,
                purchaseOrderId: purchaseOrder.Id, unitCost: item.UnitCost, ct: ct);
        }

        var fullyReceived = purchaseOrder.Items.All(i => i.ReceivedQuantity >= i.Quantity);
        purchaseOrder.Status = fullyReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        if (fullyReceived) purchaseOrder.ReceivedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
