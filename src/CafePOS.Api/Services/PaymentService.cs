using CafePOS.Api.Data;
using CafePOS.Api.Hubs;
using CafePOS.Core.BusinessRules;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

public class PaymentService
{
    private readonly AppDbContext _db;
    private readonly ShiftService _shifts;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;
    private readonly IHubContext<OrderHub> _hub;
    private readonly Security.CurrentUser _currentUser;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext db, ShiftService shifts, InventoryService inventory, AuditService audit,
        IHubContext<OrderHub> hub, Security.CurrentUser currentUser, ILogger<PaymentService> logger)
    {
        _db = db;
        _shifts = shifts;
        _inventory = inventory;
        _audit = audit;
        _hub = hub;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<OrderDto> CheckoutAsync(Guid orderId, CheckoutRequest request, CancellationToken ct = default)
    {
        if (request.Payments is null || request.Payments.Count == 0)
            throw new InvalidOperationException("Add at least one payment.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Hold the order row for the whole payment. A second click/terminal waits and
            // then sees the fresh paid state instead of failing with a concurrency 409.
            await LockOrderAsync(orderId, ct);
            var order = await _db.Orders.Include(o => o.Items).ThenInclude(i => i.Modifiers)
                .Include(o => o.Payments).Include(o => o.Refunds).Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new KeyNotFoundException("Order not found.");
            if (order.TableId.HasValue) await LockTableAsync(order.TableId.Value, ct);
            if (order.Status == OrderStatus.Cancelled)
                throw new InvalidOperationException("Cancelled orders cannot be paid.");
            if (order.Status == OrderStatus.Completed)
                throw new InvalidOperationException("This order is already paid and completed.");

            var shift = await _shifts.GetOpenShiftAsync(_currentUser.UserId, ct)
                ?? throw new InvalidOperationException("Open a cashier shift before taking payments.");

            var remaining = Money.Round(order.Total - order.PaidAmount);
            if (remaining <= 0 && request.CompleteOrder)
                throw new InvalidOperationException("This order is already fully paid.");

            decimal appliedTotal = 0;
            decimal changeTotal = 0;
            foreach (var payment in request.Payments)
            {
                if (payment.Amount <= 0) throw new InvalidOperationException("Payment amount must be greater than zero.");

                if (payment.Method == PaymentMethod.Cash)
                {
                    var change = OrderPricing.Change(payment.Amount, remaining);
                    var applied = Money.Round(payment.Amount - change);
                    if (applied <= 0) throw new InvalidOperationException("Cash tendered does not cover any part of the bill.");
                    appliedTotal += applied;
                    changeTotal += change;
                    remaining = Money.Round(remaining - applied);
                    _db.Payments.Add(new Payment
                    {
                        OrderId = order.Id,
                        Method = payment.Method,
                        Amount = applied,
                        ChangeGiven = change,
                        Reference = payment.Reference,
                        CashierId = _currentUser.UserId,
                        ShiftId = shift.Id
                    });
                }
                else
                {
                    if (payment.Amount > remaining + 0.01m)
                        throw new InvalidOperationException("Card / transfer payment cannot exceed the remaining balance.");
                    var applied = Money.Round(Math.Min(payment.Amount, remaining));
                    appliedTotal += applied;
                    remaining = Money.Round(remaining - applied);
                    _db.Payments.Add(new Payment
                    {
                        OrderId = order.Id,
                        Method = payment.Method,
                        Amount = applied,
                        ChangeGiven = 0,
                        Reference = payment.Reference,
                        CashierId = _currentUser.UserId,
                        ShiftId = shift.Id
                    });
                }
            }

            order.PaidAmount = Money.Round(order.PaidAmount + appliedTotal);
            order.ChangeDue = Money.Round(order.ChangeDue + changeTotal);

            if (request.CompleteOrder)
            {
                if (order.PaidAmount + 0.01m < order.Total)
                    throw new InvalidOperationException($"The bill is not fully paid. Remaining: {Money.Format(order.Total - order.PaidAmount)}.");
                if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Completed))
                    throw new InvalidOperationException($"An order in {order.Status} status cannot be completed.");

                order.Status = OrderStatus.Completed;
                order.CompletedAt = DateTimeOffset.UtcNow;

                if (order.TableId.HasValue)
                {
                    var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == order.TableId.Value, ct);
                    if (table is not null && table.CurrentOrderId == order.Id)
                    {
                        table.Status = TableStatus.Available;
                        table.CurrentOrderId = null;
                    }
                }

                var deductionEvent = await _db.AppSettings.AsNoTracking()
                    .Where(s => s.Key == SettingKeys.InventoryDeductionEvent).Select(s => s.Value).FirstOrDefaultAsync(ct);
                if (InventoryPolicy.ShouldDeduct(OrderStatus.Completed, deductionEvent))
                    await _inventory.DeductForOrderAsync(order, _currentUser.UserId, ct);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.LogAsync(request.CompleteOrder ? "OrderPaid" : "PartialPayment", nameof(Order), order.Id.ToString(),
                new { order.OrderNumber, Applied = appliedTotal, Change = changeTotal, order.PaidAmount, order.Total }, ct);

            var dto = await LoadDtoAsync(order.Id, ct);
            await BroadcastAsync("OrderStatusChanged", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<OrderDto> RefundAsync(Guid orderId, RefundRequest request, CancellationToken ct = default)
    {
        if (!_currentUser.IsInRole(UserRole.Manager, UserRole.Administrator))
            throw new UnauthorizedAccessException("Only a manager or administrator can authorise refunds.");
        if (request.Amount <= 0) throw new InvalidOperationException("Refund amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A refund reason is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await LockOrderAsync(orderId, ct);
            var order = await _db.Orders.Include(o => o.Payments).ThenInclude(p => p.Refunds)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new KeyNotFoundException("Order not found.");

            var payment = order.Payments.FirstOrDefault(p => p.Id == request.PaymentId)
                ?? throw new InvalidOperationException("Payment not found on this order.");

            var refundable = Money.Round(payment.Amount - payment.RefundedAmount);
            if (request.Amount > refundable + 0.01m)
                throw new InvalidOperationException($"Maximum refundable amount is {Money.Format(refundable)}.");

            var shift = payment.ShiftId.HasValue
                ? await _db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == payment.ShiftId.Value, ct)
                : await _shifts.GetOpenShiftAsync(_currentUser.UserId, ct);

            var refund = new Refund
            {
                OrderId = order.Id,
                PaymentId = payment.Id,
                Amount = request.Amount,
                Reason = request.Reason.Trim(),
                ApprovedByUserId = _currentUser.UserId,
                ShiftId = shift?.Id
            };
            _db.Refunds.Add(refund);

            payment.RefundedAmount = Money.Round(payment.RefundedAmount + request.Amount);
            payment.Status = payment.RefundedAmount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
            order.PaidAmount = Math.Max(0, Money.Round(order.PaidAmount - request.Amount));

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.LogAsync("RefundIssued", nameof(Order), order.Id.ToString(),
                new { order.OrderNumber, request.Amount, request.Reason, PaymentId = payment.Id }, ct);

            var dto = await LoadDtoAsync(order.Id, ct);
            await BroadcastAsync("OrderStatusChanged", dto, ct);
            return dto;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task LockOrderAsync(Guid id, CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    private async Task LockTableAsync(Guid id, CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"DiningTables\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    private async Task<OrderDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Table).Include(o => o.Cashier)
            .Include(o => o.Items).ThenInclude(i => i.Modifiers)
            .Include(o => o.Payments).Include(o => o.Refunds)
            .FirstAsync(o => o.Id == id, ct);
        return Mapping.ToDto(order);
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
