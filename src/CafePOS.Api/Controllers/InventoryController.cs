using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ReportService _reports;

    public InventoryController(ReportService reports) => _reports = reports;

    [HttpGet("low-stock")]
    public async Task<ActionResult<List<LowStockDto>>> LowStock(CancellationToken ct) =>
        Ok(await _reports.LowStockAsync(ct));

    [HttpGet("movements")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<List<InventoryMovementReportDto>>> Movements([FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] Guid? ingredientId, CancellationToken ct)
    {
        var end = (to ?? DateTimeOffset.UtcNow.AddDays(1)).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        return Ok(await _reports.InventoryMovementsAsync(start, end, ingredientId, ct));
    }
}
