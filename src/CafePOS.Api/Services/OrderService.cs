using CafePOS.Api.Data;
using CafePOS.Api.Hubs;
using CafePOS.Core.BusinessRules;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

public class OrderService
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;
    private readonly IHubContext<OrderHub> _hub;
    private readonly Security.CurrentUser _currentUser;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, InventoryService inventory, AuditService audit, IHubContext<OrderHub> hub,
        Security.CurrentUser currentUser, ILogger<OrderService> logger)
    {
        _db = db;
        _inventory = inventory;
        _audit = audit;
        _hub = hub;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        ValidateHeader(request.Type, request.TableId, request.CustomerName, request.DeliveryAddress);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(request.ClientOrderId))
            {
                var existing = await _db.Orders.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClientOrderId == request.ClientOrderId, ct);
                if (existing is not null)
                {
                    await tx.RollbackAsync(ct);
                    return await GetByIdAsync(existing.Id, ct);
                }
            }

            var shift = await _db.Shifts.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId && s.Status == ShiftStatus.Open, ct);

            var order = new Order
            {
                Type = request.Type,
                TableId = request.Type == OrderType.DineIn ? request.TableId : null,
                CashierId = _currentUser.UserId,
                ShiftId = shift?.Id,
                CustomerName = request.CustomerName?.Trim(),
                CustomerPhone = request.CustomerPhone?.Trim(),
                DeliveryAddress = request.DeliveryAddress?.Trim(),
                Notes = request.Notes?.Trim(),
                ClientOrderId = request.ClientOrderId,
                Status = request.SendToKitchen ? OrderStatus.Pending : OrderStatus.Draft
            };

            var seq = await _db.NextNumberAsync("order", ct);
            order.OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{seq:D4}";

            await BuildItemsAsync(order, request.Items, request.DiscountType, request.DiscountValue,
                request.DiscountId, request.ServiceChargeRate, ct);

            if (order.Status == OrderStatus.Pending) order.PlacedAt = DateTimeOffset.UtcNow;

            if (order.TableId.HasValue)
            {
                await LockTablesAsync(ct, order.TableId.Value);
                var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == order.TableId.Value, ct)
                    ?? throw new InvalidOperationException("Selected table no longer exists.");
                if (table.Status == TableStatus.Occupied && table.CurrentOrderId != order.Id)
                    throw new InvalidOperationException($"Table {table.Number} is already occupied.");
                table.Status = TableStatus.Occupied;
                table.CurrentOrderId = order.Id;
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = await GetByIdAsync(order.Id, ct);
            if (order.Status == OrderStatus.Pending)
                await BroadcastAsync("OrderCreated", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> UpdateAsync(Guid id, UpdateOrderRequest request, CancellationToken ct = default)
    {
        ValidateHeader(request.Type, request.TableId, request.CustomerName, request.DeliveryAddress);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await LoadTrackedAsync(id, ct);
            if (!OrderStateMachine.IsEditable(order.Status))
                throw new InvalidOperationException($"An order in {order.Status} status can no longer be edited.");

            var oldTableId = order.TableId;

            order.Type = request.Type;
            order.CustomerName = request.CustomerName?.Trim();
            order.CustomerPhone = request.CustomerPhone?.Trim();
            order.DeliveryAddress = request.DeliveryAddress?.Trim();
            order.Notes = request.Notes?.Trim();

            _db.OrderItems.RemoveRange(order.Items.ToList());
            order.Items.Clear();

            // Build the replacement items and update the order totals before the single
            // SaveChanges below. Saving the order twice in one transaction reused the old
            // PostgreSQL xmin on the second write and produced a false "record was
            // changed by another user" 409 on every edit.
            await BuildItemsAsync(order, request.Items, request.DiscountType, request.DiscountValue,
                request.DiscountId, request.ServiceChargeRate, ct);

            if (request.SendToKitchen && order.Status == OrderStatus.Draft)
            {
                order.Status = OrderStatus.Pending;
                order.PlacedAt = DateTimeOffset.UtcNow;
            }

            order.TableId = request.Type == OrderType.DineIn ? request.TableId : null;

            var affectedTableIds = new[] { order.TableId, oldTableId }
                .Where(id => id.HasValue).Select(id => id!.Value).ToArray();
            if (affectedTableIds.Length > 0) await LockTablesAsync(ct, affectedTableIds);

            if (order.TableId.HasValue)
            {
                var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == order.TableId.Value, ct)
                    ?? throw new InvalidOperationException("Selected table no longer exists.");
                if (table.Status == TableStatus.Occupied && table.CurrentOrderId != order.Id)
                    throw new InvalidOperationException($"Table {table.Number} is already occupied.");
                table.Status = TableStatus.Occupied;
                table.CurrentOrderId = order.Id;
            }

            if (oldTableId.HasValue && oldTableId != order.TableId)
            {
                var oldTable = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == oldTableId.Value, ct);
                if (oldTable is not null && oldTable.CurrentOrderId == order.Id)
                {
                    oldTable.Status = TableStatus.Available;
                    oldTable.CurrentOrderId = null;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = await GetByIdAsync(id, ct);
            await BroadcastAsync("OrderUpdated", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid id, OrderStatus target, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await LoadTrackedAsync(id, ct);
            if (order.Status == target) { await tx.RollbackAsync(ct); return await GetByIdAsync(id, ct); }

            EnsureStatusPermission(order.Status, target);
            OrderStateMachine.EnsureCanTransition(order.Status, target);

            if (target == OrderStatus.Completed && order.PaidAmount + 0.01m < order.Total)
                throw new InvalidOperationException("The order must be fully paid before it can be completed.");

            var now = DateTimeOffset.UtcNow;
            switch (target)
            {
                case OrderStatus.Preparing: order.PreparingAt = now; break;
                case OrderStatus.Ready: order.ReadyAt = now; break;
                case OrderStatus.Served: order.ServedAt = now; break;
                case OrderStatus.Completed: order.CompletedAt = now; break;
            }

            var previous = order.Status;
            order.Status = target;

            if (target is OrderStatus.Completed or OrderStatus.Served)
                await MaybeDeductStockAsync(order, previous, ct);

            if (target is OrderStatus.Completed)
            {
                await FreeTableAsync(order, ct);
                await _audit.LogAsync("OrderCompleted", nameof(Order), order.Id.ToString(),
                    new { order.OrderNumber, order.Total, order.PaidAmount }, ct);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = await GetByIdAsync(id, ct);
            await BroadcastAsync("OrderStatusChanged", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> CancelAsync(Guid id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A cancellation reason is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await LoadTrackedAsync(id, ct);
            if (!OrderStateMachine.IsCancellable(order.Status))
                throw new InvalidOperationException($"An order in {order.Status} status cannot be cancelled.");
            if (order.PaidAmount > 0)
                throw new InvalidOperationException("Refund the recorded payments before cancelling this order.");
            if (_currentUser.Role == UserRole.Cashier && order.Status is not (OrderStatus.Draft or OrderStatus.Pending))
                throw new UnauthorizedAccessException("A manager must approve cancelling an order that is already preparing.");

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTimeOffset.UtcNow;
            order.CancelledByUserId = _currentUser.UserId;
            order.CancelReason = reason.Trim();

            await _inventory.ReturnForOrderAsync(order, _currentUser.UserId, ct);
            await FreeTableAsync(order, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.LogAsync("OrderCancelled", nameof(Order), order.Id.ToString(),
                new { order.OrderNumber, reason, order.Total }, ct);

            var dto = await GetByIdAsync(id, ct);
            await BroadcastAsync("OrderStatusChanged", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> MoveToTableAsync(Guid id, Guid? tableId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await LoadTrackedAsync(id, ct);
            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
                throw new InvalidOperationException("Completed or cancelled orders cannot be moved.");

            var oldTableId = order.TableId;
            var affectedTableIds = new[] { tableId, oldTableId }
                .Where(id => id.HasValue).Select(id => id!.Value).ToArray();
            if (affectedTableIds.Length > 0) await LockTablesAsync(ct, affectedTableIds);

            if (tableId.HasValue)
            {
                var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
                    ?? throw new InvalidOperationException("Table not found.");
                if (table.Status == TableStatus.Occupied && table.CurrentOrderId != order.Id)
                    throw new InvalidOperationException($"Table {table.Number} is already occupied.");
                table.Status = TableStatus.Occupied;
                table.CurrentOrderId = order.Id;
            }

            order.Type = tableId.HasValue ? OrderType.DineIn : order.Type;
            order.TableId = tableId;

            if (oldTableId.HasValue && oldTableId != tableId)
            {
                var old = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == oldTableId, ct);
                if (old is not null && old.CurrentOrderId == order.Id)
                {
                    old.Status = TableStatus.Available;
                    old.CurrentOrderId = null;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await _audit.LogAsync("OrderMoved", nameof(Order), order.Id.ToString(), new { order.OrderNumber, oldTableId, tableId }, ct);
            var dto = await GetByIdAsync(id, ct);
            await BroadcastAsync("OrderUpdated", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> MergeAsync(Guid sourceId, Guid targetId, CancellationToken ct = default)
    {
        if (!_currentUser.IsInRole(UserRole.Manager, UserRole.Administrator))
            throw new UnauthorizedAccessException("Only a manager or administrator can merge orders.");
        if (sourceId == targetId) throw new InvalidOperationException("Choose two different orders to merge.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var source = await LoadTrackedAsync(sourceId, ct);
            var target = await LoadTrackedAsync(targetId, ct);
            if (!OrderStateMachine.IsEditable(source.Status) || !OrderStateMachine.IsEditable(target.Status))
                throw new InvalidOperationException("Only unpaid orders can be merged.");

            foreach (var item in source.Items.ToList())
                item.OrderId = target.Id;

            source.Status = OrderStatus.Cancelled;
            source.CancelledAt = DateTimeOffset.UtcNow;
            source.CancelledByUserId = _currentUser.UserId;
            source.CancelReason = $"Merged into {target.OrderNumber}";
            source.Total = 0;

            await _db.SaveChangesAsync(ct);
            await FreeTableAsync(source, ct);
            await RecalculateAsync(target, await GetServiceChargeRateAsync(null, ct), ct);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            await _audit.LogAsync("OrdersMerged", nameof(Order), target.Id.ToString(),
                new { source = source.OrderNumber, target = target.OrderNumber }, ct);

            var dto = await GetByIdAsync(target.Id, ct);
            await BroadcastAsync("OrderUpdated", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await FullQuery().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("Order not found.");
        return Mapping.ToDto(order);
    }

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(OrderStatus? status, OrderType? type, DateTimeOffset? from,
        DateTimeOffset? to, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Orders.AsNoTracking().Include(o => o.Table).AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (type.HasValue) query = query.Where(o => o.Type == type.Value);
        if (from.HasValue) query = query.Where(o => o.PlacedAt >= from.Value);
        if (to.HasValue) query = query.Where(o => o.PlacedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(o => o.OrderNumber.Contains(s) ||
                                     (o.CustomerName != null && o.CustomerName.Contains(s)) ||
                                     (o.Table != null && o.Table.Number.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var orders = await query.OrderByDescending(o => o.PlacedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Type, o.Status, o.Table != null ? o.Table.Number : null,
                o.CustomerName, o.Total, o.PaidAmount, o.PlacedAt, o.Items.Count))
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>(orders, total, page, pageSize);
    }

    public async Task<List<OrderDto>> KitchenQueueAsync(PreparationStation? station, CancellationToken ct = default)
    {
        var query = FullQuery().AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready);
        if (station.HasValue)
            query = query.Where(o => o.Items.Any(i => i.PreparationStation == station.Value));

        var orders = await query.OrderBy(o => o.PlacedAt).Take(100).ToListAsync(ct);
        return orders.Select(Mapping.ToDto).ToList();
    }

    public async Task<OrderDto?> GetActiveByTableAsync(Guid tableId, CancellationToken ct = default)
    {
        var order = await FullQuery().AsNoTracking()
            .Where(o => o.TableId == tableId && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.PlacedAt).FirstOrDefaultAsync(ct);
        return order is null ? null : Mapping.ToDto(order);
    }

    // ---------- helpers ----------

    private IQueryable<Order> FullQuery() => _db.Orders
        .Include(o => o.Table)
        .Include(o => o.Cashier)
        .Include(o => o.Items).ThenInclude(i => i.Modifiers)
        .Include(o => o.Payments)
        .Include(o => o.Refunds);

    private async Task<Order> LoadTrackedAsync(Guid id, CancellationToken ct)
    {
        // Serialise edits of the same order inside the transaction. Without this two POS
        // clicks or two terminals editing one order produced a 409 "changed by another
        // user" instead of applying the second change to the fresh row.
        await LockOrderAsync(id, ct);
        return await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items).ThenInclude(i => i.Modifiers)
            .Include(o => o.Payments)
            .Include(o => o.Refunds)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("Order not found.");
    }

    private async Task LockOrderAsync(Guid id, CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    private async Task LockTablesAsync(CancellationToken ct, params Guid[] tableIds)
    {
        foreach (var id in tableIds.Where(x => x != Guid.Empty).Distinct().OrderBy(x => x))
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"DiningTables\" WHERE \"Id\" = {id} FOR UPDATE", ct);
    }

    private static void ValidateHeader(OrderType type, Guid? tableId, string? customerName, string? address)
    {
        if (type == OrderType.DineIn && !tableId.HasValue)
            throw new InvalidOperationException("Please select a table for a dine-in order.");
        if (type == OrderType.Delivery && string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("A delivery address is required for delivery orders.");
        if (type == OrderType.Delivery && string.IsNullOrWhiteSpace(customerName))
            throw new InvalidOperationException("A customer name is required for delivery orders.");
    }

    private async Task BuildItemsAsync(Order order, List<OrderItemRequest> requestedItems, DiscountType discountType,
        decimal discountValue, Guid? discountId, decimal? serviceChargeRate, CancellationToken ct)
    {
        if (requestedItems is null || requestedItems.Count == 0)
            throw new InvalidOperationException("Add at least one item to the order.");

        var productIds = requestedItems.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Modifiers)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var pricingLines = new List<PricingLine>();
        var pendingItems = new List<(OrderItemRequest Request, Product Product, List<ProductModifier> Modifiers, decimal ModifierTotal)>();
        foreach (var request in requestedItems)
        {
            if (!request.ProductId.HasValue || !products.TryGetValue(request.ProductId.Value, out var product))
                throw new InvalidOperationException("One of the selected products is no longer available.");
            if (!product.IsAvailable) throw new InvalidOperationException($"{product.Name} is currently unavailable.");
            if (request.Quantity <= 0) throw new InvalidOperationException("Item quantity must be greater than zero.");

            var modifierIds = request.ModifierIds ?? new List<Guid>();
            var selected = product.Modifiers.Where(m => modifierIds.Contains(m.Id) && !m.IsDeleted && m.IsActive).ToList();
            if (modifierIds.Count != selected.Count)
                throw new InvalidOperationException($"A modifier for {product.Name} is no longer available.");

            var modifierTotal = selected.Sum(m => m.PriceDelta);
            var lineDiscount = Money.Clamp(request.DiscountAmount, 0, product.Price * request.Quantity + modifierTotal);
            pricingLines.Add(new PricingLine(product.Price, request.Quantity, lineDiscount, product.TaxRate, modifierTotal));
            pendingItems.Add((request, product, selected, modifierTotal));
        }

        if (discountId.HasValue)
        {
            var preset = await _db.Discounts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == discountId.Value && d.IsActive, ct)
                ?? throw new InvalidOperationException("The selected discount is no longer available.");
            if (preset.RequiresManagerApproval && !_currentUser.IsInRole(UserRole.Manager, UserRole.Administrator))
                throw new UnauthorizedAccessException($"Discount '{preset.Name}' requires manager approval.");
            discountType = preset.Type;
            discountValue = preset.Value;
            order.DiscountId = preset.Id;
        }
        else
        {
            order.DiscountId = null;
        }

        var serviceRate = await GetServiceChargeRateAsync(serviceChargeRate, ct);
        var totals = OrderPricing.Calculate(pricingLines, discountType, discountValue, serviceRate);

        order.DiscountType = discountType;
        order.DiscountValue = discountValue;
        order.Subtotal = totals.Subtotal;
        order.DiscountAmount = totals.DiscountAmount;
        order.TaxAmount = totals.TaxAmount;
        order.ServiceCharge = totals.ServiceCharge;
        order.Total = totals.Total;

        for (var i = 0; i < pendingItems.Count; i++)
        {
            var (request, product, modifiers, _) = pendingItems[i];
            var calc = totals.Lines[i];
            var item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = request.Quantity,
                DiscountAmount = request.DiscountAmount,
                TaxRate = product.TaxRate,
                TaxAmount = calc.Tax,
                LineTotal = calc.Total,
                Notes = request.Notes?.Trim(),
                PreparationStation = product.PreparationStation
            };
            foreach (var modifier in modifiers)
                item.Modifiers.Add(new OrderItemModifier { Name = modifier.Name, Price = modifier.PriceDelta });
            // New items already have a generated GUID key. When the parent order is already
            // tracked, adding the item only to the navigation makes EF treat it as an
            // existing row (Modified) and the UPDATE hits zero rows. Explicitly add the
            // graph so the replacement items are INSERTed.
            _db.OrderItems.Add(item);
            order.Items.Add(item);
        }
    }

    private async Task RecalculateAsync(Order order, decimal serviceRate, CancellationToken ct)
    {
        var lines = order.Items.Where(i => !i.IsDeleted)
            .Select(i => new PricingLine(i.UnitPrice, i.Quantity, i.DiscountAmount, i.TaxRate,
                i.Modifiers.Sum(m => m.Price))).ToList();
        var totals = OrderPricing.Calculate(lines, order.DiscountType, order.DiscountValue, serviceRate);
        order.Subtotal = totals.Subtotal;
        order.DiscountAmount = totals.DiscountAmount;
        order.TaxAmount = totals.TaxAmount;
        order.ServiceCharge = totals.ServiceCharge;
        order.Total = totals.Total;
        for (var i = 0; i < order.Items.Count; i++)
        {
            var item = order.Items.ElementAt(i);
            if (i >= totals.Lines.Count) break;
            item.TaxAmount = totals.Lines[i].Tax;
            item.LineTotal = totals.Lines[i].Total;
        }
        await Task.CompletedTask;
    }

    private async Task<decimal> GetServiceChargeRateAsync(decimal? requested, CancellationToken ct)
    {
        if (requested.HasValue) return Money.Clamp(requested.Value, 0, 100);
        var raw = await _db.AppSettings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.ServiceChargeRate).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate)
            ? Money.Clamp(rate, 0, 100)
            : 0;
    }

    private async Task MaybeDeductStockAsync(Order order, OrderStatus previous, CancellationToken ct)
    {
        var deductionEvent = await _db.AppSettings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.InventoryDeductionEvent).Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (InventoryPolicy.ShouldDeduct(order.Status, deductionEvent))
            await _inventory.DeductForOrderAsync(order, _currentUser.UserId, ct);
        else if (deductionEvent == InventoryPolicy.DeductionOnServed && previous == OrderStatus.Served)
            await _inventory.DeductForOrderAsync(order, _currentUser.UserId, ct);
    }

    private void EnsureStatusPermission(OrderStatus from, OrderStatus to)
    {
        if (_currentUser.IsInRole(UserRole.Manager, UserRole.Administrator)) return;
        if (_currentUser.Role == UserRole.Kitchen)
        {
            var allowed = (from, to) switch
            {
                (OrderStatus.Pending, OrderStatus.Preparing) => true,
                (OrderStatus.Preparing, OrderStatus.Ready) => true,
                (OrderStatus.Ready, OrderStatus.Served) => true,
                _ => false
            };
            if (!allowed) throw new UnauthorizedAccessException("Kitchen staff can only advance preparation status.");
            return;
        }
        if (_currentUser.Role == UserRole.Cashier)
        {
            if (to == OrderStatus.Completed) return; // checked for payment above
            if (to == OrderStatus.Served) return;
            throw new UnauthorizedAccessException("This status change requires a manager.");
        }
    }

    private async Task FreeTableAsync(Order order, CancellationToken ct)
    {
        if (!order.TableId.HasValue) return;
        await LockTablesAsync(ct, order.TableId.Value);
        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == order.TableId.Value, ct);
        if (table is not null && table.CurrentOrderId == order.Id)
        {
            table.Status = TableStatus.Available;
            table.CurrentOrderId = null;
        }
    }

    private async Task BroadcastAsync(string method, object payload, CancellationToken ct)
    {
        try
        {
            await _hub.Clients.Groups(OrderHub.CashierGroup, OrderHub.KitchenGroup, OrderHub.ManagerGroup)
                .SendAsync(method, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast {Method} failed", method);
        }
    }
}
