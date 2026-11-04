using CafePOS.Api.Data;
using CafePOS.Api.Security;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;
    private readonly CurrentUser _currentUser;

    public PurchaseOrdersController(AppDbContext db, InventoryService inventory, AuditService audit, CurrentUser currentUser)
    {
        _db = db;
        _inventory = inventory;
        _audit = audit;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<List<PurchaseOrderDto>>> List([FromQuery] PurchaseOrderStatus? status,
        [FromQuery] Guid? supplierId, CancellationToken ct)
    {
        var query = _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Ingredient)
            .AsQueryable();

        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId.Value);

        var orders = await query.OrderByDescending(p => p.OrderedAt).Take(300).ToListAsync(ct);
        var userIds = orders.Select(o => o.CreatedByUserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return Ok(orders.Select(o => ToDto(o, users.GetValueOrDefault(o.CreatedByUserId, "Unknown"))).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<PurchaseOrderDto>> Get(Guid id, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (order is null) return NotFound();
        var creator = await _db.Users.AsNoTracking().Where(u => u.Id == order.CreatedByUserId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Unknown";
        return Ok(ToDto(order, creator));
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<PurchaseOrderDto>> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new ApiErrorResponse("Add at least one ingredient to the purchase order."));
        if (request.Items.Any(i => i.Quantity <= 0 || i.UnitCost < 0))
            return BadRequest(new ApiErrorResponse("Quantities must be greater than zero and costs cannot be negative."));
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
            return BadRequest(new ApiErrorResponse("The selected supplier does not exist."));

        var ingredientIds = request.Items.Select(i => i.IngredientId).Distinct().ToList();
        if (await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id), ct) != ingredientIds.Count)
            return BadRequest(new ApiErrorResponse("One of the selected ingredients does not exist."));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var seq = await _db.NextNumberAsync("purchase", ct);
            var order = new PurchaseOrder
            {
                Number = $"PO-{DateTime.Now:yyyyMMdd}-{seq:D4}",
                SupplierId = request.SupplierId,
                Notes = request.Notes?.Trim(),
                Status = PurchaseOrderStatus.Ordered,
                CreatedByUserId = _currentUser.UserId
            };
            foreach (var item in request.Items)
            {
                order.Items.Add(new PurchaseOrderItem
                {
                    IngredientId = item.IngredientId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    LineTotal = Math.Round(item.Quantity * item.UnitCost, 2)
                });
            }
            order.Total = order.Items.Sum(i => i.LineTotal);
            _db.PurchaseOrders.Add(order);
            await _db.SaveChangesAsync(ct);

            var supplier = await _db.Suppliers.FirstAsync(s => s.Id == request.SupplierId, ct);
            supplier.Balance = Math.Round(supplier.Balance + order.Total, 2);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await _audit.LogAsync("PurchaseOrderCreated", nameof(PurchaseOrder), order.Id.ToString(),
                new { order.Number, order.Total }, ct);

            var full = await _db.PurchaseOrders.AsNoTracking().Include(p => p.Supplier).Include(p => p.Items)
                .ThenInclude(i => i.Ingredient).FirstAsync(p => p.Id == order.Id, ct);
            return Ok(ToDto(full, _currentUser.Name));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    [HttpPost("{id:guid}/receive")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<PurchaseOrderDto>> Receive(Guid id, [FromBody] ReceivePurchaseOrderRequest request,
        CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.Include(p => p.Items).ThenInclude(i => i.Ingredient)
            .Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (order is null) return NotFound();
        if (order.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Received)
            return BadRequest(new ApiErrorResponse("This purchase order can no longer be received."));

        var requested = (request.Items ?? new List<ReceivePurchaseOrderItemRequest>())
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        if (requested.Any(x => x.Value < 0))
            return BadRequest(new ApiErrorResponse("Received quantities cannot be negative."));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _inventory.ReceivePurchaseOrderAsync(order, requested, _currentUser.UserId, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await _audit.LogAsync("PurchaseOrderReceived", nameof(PurchaseOrder), order.Id.ToString(),
            new { order.Number, order.Status }, ct);

        var full = await _db.PurchaseOrders.AsNoTracking().Include(p => p.Supplier).Include(p => p.Items)
            .ThenInclude(i => i.Ingredient).FirstAsync(p => p.Id == order.Id, ct);
        var creator = await _db.Users.AsNoTracking().Where(u => u.Id == full.CreatedByUserId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Unknown";
        return Ok(ToDto(full, creator));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (order is null) return NotFound();
        if (order.Items.Any(i => i.ReceivedQuantity > 0))
            return BadRequest(new ApiErrorResponse("Partially received purchase orders cannot be cancelled."));

        order.Status = PurchaseOrderStatus.Cancelled;
        order.Notes = $"{order.Notes}\nCancelled: {request.Reason}".Trim();
        var supplier = await _db.Suppliers.FirstAsync(s => s.Id == order.SupplierId, ct);
        supplier.Balance = Math.Max(0, Math.Round(supplier.Balance - order.Total, 2));
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PurchaseOrderCancelled", nameof(PurchaseOrder), id.ToString(), new { request.Reason }, ct);
        var creator = await _db.Users.AsNoTracking().Where(u => u.Id == order.CreatedByUserId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Unknown";
        return Ok(ToDto(order, creator));
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder order, string creator) => new(
        order.Id, order.Number, order.SupplierId, order.Supplier?.Name ?? string.Empty, order.Status,
        order.OrderedAt, order.ReceivedAt, order.Notes, order.Total,
        order.Items.Select(i => new PurchaseOrderItemDto(i.Id, i.IngredientId, i.Ingredient?.Name ?? string.Empty,
            i.Ingredient?.Unit ?? string.Empty, i.Quantity, i.UnitCost, i.ReceivedQuantity, i.LineTotal)).ToList(),
        creator);
}
