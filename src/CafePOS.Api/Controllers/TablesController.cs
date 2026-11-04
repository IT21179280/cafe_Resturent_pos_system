using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/tables")]
[Authorize]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public TablesController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<List<TableDto>>> List(CancellationToken ct)
    {
        var tables = await _db.DiningTables.AsNoTracking().OrderBy(t => t.DisplayOrder).ThenBy(t => t.Number).ToListAsync(ct);
        var orderNumbers = await _db.Orders.AsNoTracking()
            .Where(o => o.TableId != null && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.TableId, o.OrderNumber })
            .ToListAsync(ct);
        var lookup = orderNumbers.Where(o => o.TableId.HasValue).GroupBy(o => o.TableId!.Value)
            .ToDictionary(g => g.Key, g => g.First().OrderNumber);
        return Ok(tables.Select(t => Mapping.ToDto(t, t.CurrentOrderId.HasValue && lookup.TryGetValue(t.Id, out var n) ? n : null)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<TableDto>> Create([FromBody] SaveTableRequest request, CancellationToken ct)
    {
        var number = request.Number.Trim();
        if (await _db.DiningTables.AnyAsync(t => t.Number.ToLower() == number.ToLower(), ct))
            return Conflict(new ApiErrorResponse("A table with this number already exists."));

        var table = new DiningTable
        {
            Number = number,
            Capacity = request.Capacity,
            Area = request.Area?.Trim(),
            DisplayOrder = request.DisplayOrder,
            Status = TableStatus.Available
        };
        _db.DiningTables.Add(table);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("TableCreated", nameof(DiningTable), table.Id.ToString(), new { table.Number }, ct);
        return Ok(Mapping.ToDto(table));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<TableDto>> Update(Guid id, [FromBody] SaveTableRequest request, CancellationToken ct)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null) return NotFound();
        var number = request.Number.Trim();
        if (await _db.DiningTables.AnyAsync(t => t.Id != id && t.Number.ToLower() == number.ToLower(), ct))
            return Conflict(new ApiErrorResponse("A table with this number already exists."));

        table.Number = number;
        table.Capacity = request.Capacity;
        table.Area = request.Area?.Trim();
        table.DisplayOrder = request.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("TableUpdated", nameof(DiningTable), table.Id.ToString(), new { table.Number }, ct);
        return Ok(Mapping.ToDto(table));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TableDto>> SetStatus(Guid id, [FromBody] SetTableStatusRequest request, CancellationToken ct)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null) return NotFound();
        if (table.Status == TableStatus.Occupied && request.Status != TableStatus.Occupied && table.CurrentOrderId.HasValue)
        {
            var hasActiveOrder = await _db.Orders.AnyAsync(o => o.Id == table.CurrentOrderId &&
                o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled, ct);
            if (hasActiveOrder)
                return Conflict(new ApiErrorResponse("This table has an open order. Complete or move the order first."));
            table.CurrentOrderId = null;
        }
        if (request.Status == TableStatus.Available) table.CurrentOrderId = null;
        table.Status = request.Status;
        await _db.SaveChangesAsync(ct);
        return Ok(Mapping.ToDto(table));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null) return NotFound();
        if (table.Status == TableStatus.Occupied)
            return Conflict(new ApiErrorResponse("An occupied table cannot be deleted."));
        _db.DiningTables.Remove(table);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("TableDeleted", nameof(DiningTable), id.ToString(), new { table.Number }, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/active-order")]
    public async Task<IActionResult> ActiveOrder(Guid id, [FromServices] OrderService orders, CancellationToken ct)
    {
        var order = await orders.GetActiveByTableAsync(id, ct);
        return order is null ? NoContent() : Ok(order);
    }
}
