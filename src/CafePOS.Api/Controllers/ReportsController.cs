using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reports;
    private readonly AppDbContext _db;

    public ReportsController(ReportService reports, AppDbContext db)
    {
        _reports = reports;
        _db = db;
    }

    [HttpGet("{type}")]
    public async Task<IActionResult> Get(string type, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] string? format, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        var endLocal = to ?? DateTimeOffset.Now.AddDays(1);
        var startLocal = from ?? endLocal.AddDays(-30);
        var end = endLocal.ToUniversalTime();
        var start = startLocal.ToUniversalTime();
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");

        switch (type.ToLowerInvariant())
        {
            case "sales":
            case "sales-daily":
            {
                var rows = await _reports.SalesByDayAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"sales-by-day-{stamp}.csv") : Ok(rows);
            }
            case "sales-cashier":
            {
                var rows = await _reports.SalesByCashierAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"sales-by-cashier-{stamp}.csv") : Ok(rows);
            }
            case "sales-payment":
            {
                var rows = await _reports.SalesByPaymentAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"sales-by-payment-{stamp}.csv") : Ok(rows);
            }
            case "sales-category":
            {
                var rows = await _reports.SalesByCategoryAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"sales-by-category-{stamp}.csv") : Ok(rows);
            }
            case "best-sellers":
            {
                var rows = await _reports.BestSellersAsync(start, end, Math.Clamp(take, 1, 200), ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"best-sellers-{stamp}.csv") : Ok(rows);
            }
            case "low-stock":
            {
                var rows = await _reports.LowStockAsync(ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"low-stock-{stamp}.csv") : Ok(rows);
            }
            case "inventory-movements":
            {
                var rows = await _reports.InventoryMovementsAsync(start, end, null, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"inventory-movements-{stamp}.csv") : Ok(rows);
            }
            case "wastage":
            {
                var rows = await _reports.WastageAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"wastage-{stamp}.csv") : Ok(rows);
            }
            case "refunds":
            {
                var rows = await _reports.RefundsAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"refunds-{stamp}.csv") : Ok(rows);
            }
            case "cancellations":
            {
                var rows = await _reports.CancellationsAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"cancellations-{stamp}.csv") : Ok(rows);
            }
            case "shifts":
            {
                if (!User.IsInRole("Administrator") && !User.IsInRole("Manager"))
                    return Forbid();
                var rows = await _reports.ShiftSummariesAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"shifts-{stamp}.csv") : Ok(rows);
            }
            case "gross-profit":
            {
                if (!User.IsInRole("Administrator") && !User.IsInRole("Manager"))
                    return Forbid();
                var rows = await _reports.GrossProfitAsync(start, end, ct);
                return csv ? Utilities.CsvExport.ToCsv(rows, $"gross-profit-{stamp}.csv") : Ok(rows);
            }
            default:
                return NotFound(new ApiErrorResponse($"Unknown report '{type}'."));
        }
    }

    [HttpGet("audit-log")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> AuditLogs([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);
        if (!User.IsInRole("Administrator") && !User.IsInRole("Manager"))
            return Forbid();
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
}
