using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public SuppliersController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<List<SupplierDto>>> List([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s) || (x.Phone != null && x.Phone.Contains(s)));
        }
        var suppliers = await query.OrderBy(s => s.Name).ToListAsync(ct);
        return Ok(suppliers.Select(ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] SaveSupplierRequest request, CancellationToken ct)
    {
        if (await _db.Suppliers.AnyAsync(s => s.Name.ToLower() == request.Name.Trim().ToLower(), ct))
            return Conflict(new ApiErrorResponse("A supplier with this name already exists."));

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            Notes = request.Notes?.Trim()
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SupplierCreated", nameof(Supplier), supplier.Id.ToString(), new { supplier.Name }, ct);
        return Ok(ToDto(supplier));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, [FromBody] SaveSupplierRequest request, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return NotFound();
        if (await _db.Suppliers.AnyAsync(s => s.Id != id && s.Name.ToLower() == request.Name.Trim().ToLower(), ct))
            return Conflict(new ApiErrorResponse("A supplier with this name already exists."));

        supplier.Name = request.Name.Trim();
        supplier.ContactPerson = request.ContactPerson?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SupplierUpdated", nameof(Supplier), supplier.Id.ToString(), new { supplier.Name }, ct);
        return Ok(ToDto(supplier));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return NotFound();
        if (await _db.PurchaseOrders.AnyAsync(p => p.SupplierId == id, ct))
            return Conflict(new ApiErrorResponse("This supplier has purchase orders and cannot be deleted."));
        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SupplierDeleted", nameof(Supplier), id.ToString(), new { supplier.Name }, ct);
        return NoContent();
    }

    private static SupplierDto ToDto(Supplier s) => new(s.Id, s.Name, s.ContactPerson, s.Phone, s.Email, s.Address,
        s.Notes, s.Balance, s.IsDeleted);
}
