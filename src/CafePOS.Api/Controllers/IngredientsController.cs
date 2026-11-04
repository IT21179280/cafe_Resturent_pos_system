using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/ingredients")]
[Authorize]
public class IngredientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;

    public IngredientsController(AppDbContext db, InventoryService inventory, AuditService audit)
    {
        _db = db;
        _inventory = inventory;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<List<IngredientDto>>> List([FromQuery] string? search, [FromQuery] bool lowStock = false,
        CancellationToken ct = default)
    {
        var query = _db.Ingredients.AsNoTracking().Include(i => i.DefaultSupplier).AsQueryable();
        if (lowStock) query = query.Where(i => i.CurrentStock <= i.LowStockThreshold);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || (i.Sku != null && i.Sku.ToLower().Contains(s)));
        }
        var items = await query.OrderBy(i => i.Name).ToListAsync(ct);
        return Ok(items.Select(Mapping.ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> Get(Guid id, CancellationToken ct)
    {
        var ingredient = await _db.Ingredients.AsNoTracking().Include(i => i.DefaultSupplier)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        return ingredient is null ? NotFound() : Ok(Mapping.ToDto(ingredient));
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<IngredientDto>> Create([FromBody] SaveIngredientRequest request, CancellationToken ct)
    {
        if (await _db.Ingredients.AnyAsync(i => i.Name.ToLower() == request.Name.Trim().ToLower(), ct))
            return Conflict(new ApiErrorResponse("An ingredient with this name already exists."));

        var ingredient = new Ingredient
        {
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            Unit = request.Unit.Trim(),
            CurrentStock = request.CurrentStock,
            LowStockThreshold = request.LowStockThreshold,
            CostPerUnit = request.CostPerUnit,
            DefaultSupplierId = request.DefaultSupplierId
        };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("IngredientCreated", nameof(Ingredient), ingredient.Id.ToString(), new { ingredient.Name }, ct);
        return Ok(Mapping.ToDto(ingredient));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<IngredientDto>> Update(Guid id, [FromBody] SaveIngredientRequest request, CancellationToken ct)
    {
        var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingredient is null) return NotFound();
        if (await _db.Ingredients.AnyAsync(i => i.Id != id && i.Name.ToLower() == request.Name.Trim().ToLower(), ct))
            return Conflict(new ApiErrorResponse("An ingredient with this name already exists."));

        ingredient.Name = request.Name.Trim();
        ingredient.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        ingredient.Unit = request.Unit.Trim();
        ingredient.LowStockThreshold = request.LowStockThreshold;
        ingredient.CostPerUnit = request.CostPerUnit;
        ingredient.DefaultSupplierId = request.DefaultSupplierId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("IngredientUpdated", nameof(Ingredient), ingredient.Id.ToString(), new { ingredient.Name }, ct);
        return Ok(Mapping.ToDto(ingredient));
    }

    [HttpPost("{id:guid}/adjust")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<IngredientDto>> Adjust(Guid id, [FromBody] StockAdjustmentRequest request, CancellationToken ct)
    {
        var ingredient = await _db.Ingredients.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingredient is null) return NotFound();
        var userId = GetUserId();
        await _inventory.AdjustAsync(id, request.Quantity, request.Reason, userId, ct);
        var updated = await _db.Ingredients.AsNoTracking().Include(i => i.DefaultSupplier).FirstAsync(i => i.Id == id, ct);
        return Ok(Mapping.ToDto(updated));
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<List<InventoryMovementReportDto>>> Movements(Guid id, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromServices] ReportService reports, CancellationToken ct)
    {
        var end = (to ?? DateTimeOffset.UtcNow.AddDays(1)).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        return Ok(await reports.InventoryMovementsAsync(start, end, id, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingredient is null) return NotFound();
        if (await _db.RecipeItems.AnyAsync(r => r.IngredientId == id, ct))
            return Conflict(new ApiErrorResponse("This ingredient is used in a recipe and cannot be deleted."));
        _db.Ingredients.Remove(ingredient);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("IngredientDeleted", nameof(Ingredient), id.ToString(), new { ingredient.Name }, ct);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
