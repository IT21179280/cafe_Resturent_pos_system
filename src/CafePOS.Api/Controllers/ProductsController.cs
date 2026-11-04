using System.Globalization;
using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Api.Utilities;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

public class ProductCsvRow
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TaxRate { get; set; }
    public string PreparationStation { get; set; } = "Kitchen";
    public bool IsAvailable { get; set; } = true;
    public bool TrackInventory { get; set; }
}

[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ImageStorage _images;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(AppDbContext db, AuditService audit, ImageStorage images, ILogger<ProductsController> logger)
    {
        _db = db;
        _audit = audit;
        _images = images;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> List([FromQuery] string? search, [FromQuery] Guid? categoryId,
        [FromQuery] bool availableOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var query = _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Modifiers).AsQueryable();

        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (availableOnly) query = query.Where(p => p.IsAvailable);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) ||
                                     p.Sku.ToLower().Contains(s) ||
                                     (p.Barcode != null && p.Barcode.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new PagedResult<ProductDto>(items.Select(Mapping.ToDto).ToList(), total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Modifiers)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? NotFound() : Ok(Mapping.ToDto(product));
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<ProductDto>> Lookup([FromQuery] string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new ApiErrorResponse("Enter a barcode or SKU."));
        var c = code.Trim();
        var product = await _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Modifiers)
            .FirstOrDefaultAsync(p => p.Barcode == c || p.Sku == c, ct);
        return product is null ? NotFound() : Ok(Mapping.ToDto(product));
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] SaveProductRequest request, CancellationToken ct)
    {
        var error = await ValidateUniqueAsync(request, null, ct);
        if (error is not null) return Conflict(new ApiErrorResponse(error));

        var product = new Product();
        await ApplyAsync(product, request, ct);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ProductCreated", nameof(Product), product.Id.ToString(), new { product.Name, product.Sku }, ct);
        return Ok(await LoadDtoAsync(product.Id, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] SaveProductRequest request, CancellationToken ct)
    {
        var product = await _db.Products.Include(p => p.Modifiers).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return NotFound();

        var error = await ValidateUniqueAsync(request, id, ct);
        if (error is not null) return Conflict(new ApiErrorResponse(error));

        var oldImage = product.ImageUrl;
        await ApplyAsync(product, request, ct);
        await _db.SaveChangesAsync(ct);
        if (!string.Equals(oldImage, product.ImageUrl, StringComparison.OrdinalIgnoreCase))
            _images.TryDelete(oldImage);
        await _audit.LogAsync("ProductUpdated", nameof(Product), product.Id.ToString(), new { product.Name, product.Sku }, ct);
        return Ok(await LoadDtoAsync(product.Id, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return NotFound();
        var oldImage = product.ImageUrl;
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        _images.TryDelete(oldImage);
        await _audit.LogAsync("ProductDeleted", nameof(Product), id.ToString(), new { product.Name, product.Sku }, ct);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var products = await _db.Products.AsNoTracking().Include(p => p.Category).OrderBy(p => p.Name).ToListAsync(ct);
        var rows = products.Select(p => new ProductCsvRow
        {
            Name = p.Name,
            Sku = p.Sku,
            Barcode = p.Barcode,
            Category = p.Category?.Name ?? string.Empty,
            Description = p.Description,
            Price = p.Price,
            CostPrice = p.CostPrice,
            TaxRate = p.TaxRate,
            PreparationStation = p.PreparationStation.ToString(),
            IsAvailable = p.IsAvailable,
            TrackInventory = p.TrackInventory
        });
        return CsvExport.ToCsv(rows, $"cafepos-products-{DateTime.Now:yyyyMMdd}.csv");
    }

    [HttpPost("import")]
    [Authorize(Roles = "Administrator,Manager")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ProductImportResult>> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new ApiErrorResponse("Choose a CSV file to import."));

        var errors = new List<string>();
        var imported = 0;
        var skipped = 0;

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null
        });

        List<ProductCsvRow> rows;
        try
        {
            rows = csv.GetRecords<ProductCsvRow>().ToList();
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiErrorResponse("The CSV file could not be read.", ex.Message));
        }

        var categories = await _db.Categories.ToListAsync(ct);
        var existingSkus = await _db.Products.Select(p => p.Sku.ToLower()).ToListAsync(ct);
        var existingBarcodes = await _db.Products.Where(p => p.Barcode != null).Select(p => p.Barcode!).ToListAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2;
            if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Sku))
            {
                errors.Add($"Line {line}: name and SKU are required.");
                skipped++;
                continue;
            }

            var sku = row.Sku.Trim();
            if (existingSkus.Contains(sku.ToLower()))
            {
                errors.Add($"Line {line}: SKU '{sku}' already exists.");
                skipped++;
                continue;
            }

            var category = categories.FirstOrDefault(c => string.Equals(c.Name, row.Category.Trim(), StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                errors.Add($"Line {line}: category '{row.Category}' was not found.");
                skipped++;
                continue;
            }

            var barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode.Trim();
            if (barcode is not null && existingBarcodes.Contains(barcode))
            {
                errors.Add($"Line {line}: barcode '{barcode}' already exists.");
                skipped++;
                continue;
            }

            if (row.Price < 0)
            {
                errors.Add($"Line {line}: price cannot be negative.");
                skipped++;
                continue;
            }

            Enum.TryParse<PreparationStation>(row.PreparationStation, true, out var station);
            var product = new Product
            {
                Name = row.Name.Trim(),
                Sku = sku,
                Barcode = barcode,
                CategoryId = category.Id,
                Description = row.Description?.Trim(),
                Price = row.Price,
                CostPrice = row.CostPrice,
                TaxRate = row.TaxRate,
                PreparationStation = station,
                IsAvailable = row.IsAvailable,
                TrackInventory = row.TrackInventory
            };
            _db.Products.Add(product);
            existingSkus.Add(sku.ToLower());
            if (barcode is not null) existingBarcodes.Add(barcode);
            imported++;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await _audit.LogAsync("ProductsImported", nameof(Product), null, new { imported, skipped, errors = errors.Count });
        _logger.LogInformation("CSV import finished: {Imported} imported, {Skipped} skipped", imported, skipped);
        return Ok(new ProductImportResult(imported, skipped, errors));
    }

    [HttpGet("{id:guid}/recipe")]
    public async Task<ActionResult<List<RecipeItemDto>>> GetRecipe(Guid id, CancellationToken ct)
    {
        var recipe = await _db.RecipeItems.AsNoTracking()
            .Where(r => r.ProductId == id)
            .Select(r => new RecipeItemDto(r.Id, r.IngredientId, r.Ingredient!.Name, r.Ingredient.Unit,
                r.Quantity, r.Ingredient.CostPerUnit))
            .ToListAsync(ct);
        return Ok(recipe);
    }

    [HttpPut("{id:guid}/recipe")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<List<RecipeItemDto>>> SaveRecipe(Guid id, [FromBody] SaveRecipeRequest request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return NotFound();
        if (request.Items.Any(i => i.Quantity <= 0))
            return BadRequest(new ApiErrorResponse("Ingredient quantities must be greater than zero."));
        if (request.Items.GroupBy(i => i.IngredientId).Any(g => g.Count() > 1))
            return BadRequest(new ApiErrorResponse("An ingredient can only appear once in a recipe."));

        var existing = await _db.RecipeItems.Where(r => r.ProductId == id).ToListAsync(ct);
        _db.RecipeItems.RemoveRange(existing);
        foreach (var item in request.Items)
        {
            if (!await _db.Ingredients.AnyAsync(i => i.Id == item.IngredientId, ct))
                return BadRequest(new ApiErrorResponse("One of the selected ingredients no longer exists."));
            _db.RecipeItems.Add(new RecipeItem { ProductId = id, IngredientId = item.IngredientId, Quantity = item.Quantity });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("RecipeUpdated", nameof(Product), id.ToString(), new { Items = request.Items.Count }, ct);
        return Ok(await GetRecipe(id, ct));
    }

    private async Task<string?> ValidateUniqueAsync(SaveProductRequest request, Guid? excludeId, CancellationToken ct)
    {
        var sku = request.Sku.Trim().ToLower();
        if (await _db.Products.AnyAsync(p => p.Sku.ToLower() == sku && (!excludeId.HasValue || p.Id != excludeId), ct))
            return "A product with this SKU already exists.";
        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcode = request.Barcode.Trim();
            if (await _db.Products.AnyAsync(p => p.Barcode == barcode && (!excludeId.HasValue || p.Id != excludeId), ct))
                return "A product with this barcode already exists.";
        }
        return null;
    }

    private async Task ApplyAsync(Product product, SaveProductRequest request, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new InvalidOperationException("The selected category does not exist.");

        product.Name = request.Name.Trim();
        product.Sku = request.Sku.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        product.CategoryId = request.CategoryId;
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.CostPrice = request.CostPrice;
        product.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        product.IsAvailable = request.IsAvailable;
        product.PreparationStation = request.PreparationStation;
        product.TaxRate = request.TaxRate;
        product.TrackInventory = request.TrackInventory;

        var requested = request.Modifiers ?? new List<SaveProductModifierRequest>();
        var existing = product.Modifiers.ToList();
        foreach (var modifier in requested)
        {
            if (modifier.Id.HasValue)
            {
                var entity = existing.FirstOrDefault(m => m.Id == modifier.Id.Value);
                if (entity is null) continue;
                entity.Name = modifier.Name.Trim();
                entity.PriceDelta = modifier.PriceDelta;
                entity.IsActive = modifier.IsActive;
            }
            else if (!string.IsNullOrWhiteSpace(modifier.Name))
            {
                var created = new ProductModifier
                {
                    ProductId = product.Id,
                    Name = modifier.Name.Trim(),
                    PriceDelta = modifier.PriceDelta,
                    IsActive = modifier.IsActive
                };
                product.Modifiers.Add(created);
                // The entity has a client-generated Guid key, so adding it to the navigation
                // collection alone can be detected as Modified. Track it explicitly as Added.
                _db.ProductModifiers.Add(created);
            }
        }

        var keep = requested.Where(m => m.Id.HasValue).Select(m => m.Id!.Value).ToHashSet();
        foreach (var removed in existing.Where(m => !keep.Contains(m.Id)))
            _db.ProductModifiers.Remove(removed);

        await Task.CompletedTask;
    }

    private async Task<ProductDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Modifiers)
            .FirstAsync(p => p.Id == id, ct);
        return Mapping.ToDto(product);
    }
}
