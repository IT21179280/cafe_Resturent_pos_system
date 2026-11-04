using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ImageStorage _images;

    public CategoriesController(AppDbContext db, AuditService audit, ImageStorage images)
    {
        _db = db;
        _audit = audit;
        _images = images;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> List([FromQuery] bool includeInactive = true, CancellationToken ct = default)
    {
        var query = _db.Categories.AsNoTracking().Include(c => c.Products).AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        var categories = await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);
        return Ok(categories.Select(Mapping.ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] SaveCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
            return Conflict(new ApiErrorResponse("A category with this name already exists."));

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            ImageUrl = NormalizeImageUrl(request.ImageUrl)
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CategoryCreated", nameof(Category), category.Id.ToString(), new { category.Name }, ct);
        return Ok(Mapping.ToDto(category));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, [FromBody] SaveCategoryRequest request, CancellationToken ct)
    {
        var category = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            return Conflict(new ApiErrorResponse("A category with this name already exists."));

        var oldImage = category.ImageUrl;
        category.Name = name;
        category.Description = request.Description?.Trim();
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.ImageUrl = NormalizeImageUrl(request.ImageUrl);
        await _db.SaveChangesAsync(ct);
        if (!string.Equals(oldImage, category.ImageUrl, StringComparison.OrdinalIgnoreCase))
            _images.TryDelete(oldImage);
        await _audit.LogAsync("CategoryUpdated", nameof(Category), category.Id.ToString(), new { category.Name }, ct);
        return Ok(Mapping.ToDto(category));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var category = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();
        if (category.Products.Any(p => !p.IsDeleted))
            return Conflict(new ApiErrorResponse("Move or delete the products in this category first."));

        var oldImage = category.ImageUrl;
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        _images.TryDelete(oldImage);
        await _audit.LogAsync("CategoryDeleted", nameof(Category), id.ToString(), new { category.Name }, ct);
        return NoContent();
    }

    private static string? NormalizeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
}
