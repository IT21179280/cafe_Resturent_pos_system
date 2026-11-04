using CafePOS.Api.Data;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

public class ReportService
{
    private readonly AppDbContext _db;
    private readonly ShiftService _shifts;

    public ReportService(AppDbContext db, ShiftService shifts)
    {
        _db = db;
        _shifts = shifts;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var (start, end) = TodayRange();

        var completed = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end)
            .Select(o => new { o.Id, o.Total, o.CompletedAt })
            .ToListAsync(ct);

        var todaySales = completed.Sum(o => o.Total);
        var orderCount = completed.Count;
        var average = orderCount == 0 ? 0 : Math.Round(todaySales / orderCount, 2, MidpointRounding.AwayFromZero);

        var openTables = await _db.DiningTables.CountAsync(t => t.Status == TableStatus.Occupied, ct);
        var pendingKitchen = await _db.Orders.CountAsync(o =>
            o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing, ct);

        var topProducts = await (from oi in _db.OrderItems.AsNoTracking()
                                 join o in _db.Orders.AsNoTracking() on oi.OrderId equals o.Id
                                 where o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end
                                 group new { oi.Quantity, oi.LineTotal } by oi.ProductName into g
                                 orderby g.Sum(x => x.Quantity) descending
                                 select new TopProductDto(null, g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .Take(5).ToListAsync(ct);

        var lowStock = await _db.Ingredients.AsNoTracking()
            .Where(i => i.CurrentStock <= i.LowStockThreshold)
            .OrderBy(i => i.CurrentStock - i.LowStockThreshold)
            .Take(10)
            .Select(i => new LowStockDto(i.Id, i.Name, i.Unit, i.CurrentStock, i.LowStockThreshold))
            .ToListAsync(ct);

        var recentOrders = await _db.Orders.AsNoTracking().Include(o => o.Table)
            .OrderByDescending(o => o.PlacedAt).Take(8)
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Type, o.Status,
                o.Table != null ? o.Table.Number : null, o.CustomerName, o.Total, o.PaidAmount, o.PlacedAt, o.Items.Count))
            .ToListAsync(ct);

        var hourly = new List<HourlySalesDto>();
        for (var hour = 0; hour < 24; hour++)
        {
            var sales = completed.Where(o => o.CompletedAt.HasValue && o.CompletedAt.Value.ToLocalTime().Hour == hour)
                .Sum(o => o.Total);
            var count = completed.Count(o => o.CompletedAt.HasValue && o.CompletedAt.Value.ToLocalTime().Hour == hour);
            hourly.Add(new HourlySalesDto(hour, sales, count));
        }

        return new DashboardDto(todaySales, orderCount, average, openTables, pendingKitchen,
            topProducts, lowStock, recentOrders, hourly);
    }

    public async Task<List<SalesByDayDto>> SalesByDayAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var orders = await CompletedOrdersAsync(start, end, ct);
        return orders
            .GroupBy(o => o.CompletedAt!.Value.ToLocalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g => new SalesByDayDto(DateOnly.FromDateTime(g.Key), g.Count(), g.Sum(o => o.Total),
                g.Sum(o => o.DiscountAmount), g.Sum(o => o.TaxAmount), g.Sum(o => o.Total)))
            .ToList();
    }

    public async Task<List<SalesByCashierDto>> SalesByCashierAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Cashier)
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end)
            .ToListAsync(ct);
        return orders.GroupBy(o => o.Cashier?.FullName ?? o.Cashier?.UserName ?? "Unknown")
            .Select(g => new SalesByCashierDto(g.Key, g.Count(), g.Sum(o => o.Total)))
            .OrderByDescending(x => x.Sales).ToList();
    }

    public async Task<List<SalesByPaymentDto>> SalesByPaymentAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var rows = await (from p in _db.Payments.AsNoTracking()
                          join o in _db.Orders.AsNoTracking() on p.OrderId equals o.Id
                          where o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end
                          select new { p.Method, p.Amount }).ToListAsync(ct);
        return rows.GroupBy(p => p.Method)
            .Select(g => new SalesByPaymentDto(g.Key.ToString(), g.Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount).ToList();
    }

    public async Task<List<SalesByCategoryDto>> SalesByCategoryAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default) =>
        await (from oi in _db.OrderItems.AsNoTracking()
               join o in _db.Orders.AsNoTracking() on oi.OrderId equals o.Id
               join p in _db.Products.AsNoTracking() on oi.ProductId equals p.Id
               join c in _db.Categories.AsNoTracking() on p.CategoryId equals c.Id
               where o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end
               group new { oi.Quantity, oi.LineTotal } by c.Name into g
               select new SalesByCategoryDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.Sales).ToListAsync(ct);

    public async Task<List<BestSellerDto>> BestSellersAsync(DateTimeOffset start, DateTimeOffset end, int take = 20, CancellationToken ct = default) =>
        await (from oi in _db.OrderItems.AsNoTracking()
               join o in _db.Orders.AsNoTracking() on oi.OrderId equals o.Id
               where o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end
               group new { oi.Quantity, oi.LineTotal } by oi.ProductName into g
               orderby g.Sum(x => x.Quantity) descending
               select new BestSellerDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .Take(take).ToListAsync(ct);

    public async Task<List<LowStockDto>> LowStockAsync(CancellationToken ct = default) =>
        await _db.Ingredients.AsNoTracking()
            .Where(i => i.CurrentStock <= i.LowStockThreshold)
            .OrderBy(i => i.CurrentStock - i.LowStockThreshold)
            .Select(i => new LowStockDto(i.Id, i.Name, i.Unit, i.CurrentStock, i.LowStockThreshold))
            .ToListAsync(ct);

    public async Task<List<InventoryMovementReportDto>> InventoryMovementsAsync(DateTimeOffset start, DateTimeOffset end,
        Guid? ingredientId, CancellationToken ct = default)
    {
        var query = from m in _db.InventoryMovements.AsNoTracking()
                    join i in _db.Ingredients.AsNoTracking() on m.IngredientId equals i.Id
                    join u in _db.Users.AsNoTracking() on m.UserId equals u.Id into users
                    from u in users.DefaultIfEmpty()
                    where m.OccurredAt >= start && m.OccurredAt < end
                    select new { m, i.Name, i.Unit, UserName = u != null ? u.FullName : null };
        if (ingredientId.HasValue) query = query.Where(x => x.m.IngredientId == ingredientId.Value);

        var rows = await query.OrderByDescending(x => x.m.OccurredAt).Take(1000).ToListAsync(ct);
        return rows.Select(x => new InventoryMovementReportDto(x.m.OccurredAt, x.Name, x.m.Type.ToString(), x.m.Quantity,
            x.Unit, Math.Round(Math.Abs(x.m.Quantity) * x.m.UnitCost, 2), x.m.Reason, x.UserName)).ToList();
    }

    public async Task<List<WastageDto>> WastageAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var rows = await (from m in _db.InventoryMovements.AsNoTracking()
                          join i in _db.Ingredients.AsNoTracking() on m.IngredientId equals i.Id
                          where m.Type == InventoryMovementType.Wastage && m.OccurredAt >= start && m.OccurredAt < end
                          orderby m.OccurredAt descending
                          select new { m.OccurredAt, i.Name, m.Quantity, i.Unit, m.Reason, m.UnitCost })
            .Take(500).ToListAsync(ct);
        return rows.Select(x => new WastageDto(x.OccurredAt, x.Name, Math.Abs(x.Quantity), x.Unit, x.Reason,
            Math.Round(Math.Abs(x.Quantity) * x.UnitCost, 2))).ToList();
    }

    public async Task<List<RefundReportDto>> RefundsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default) =>
        await (from r in _db.Refunds.AsNoTracking()
               join o in _db.Orders.AsNoTracking() on r.OrderId equals o.Id
               join u in _db.Users.AsNoTracking() on r.ApprovedByUserId equals u.Id
               where r.RefundedAt >= start && r.RefundedAt < end
               orderby r.RefundedAt descending
               select new RefundReportDto(o.OrderNumber, r.Amount, r.Reason, u.FullName, r.RefundedAt))
            .ToListAsync(ct);

    public async Task<List<CancellationReportDto>> CancellationsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default) =>
        await (from o in _db.Orders.AsNoTracking()
               join u in _db.Users.AsNoTracking() on o.CashierId equals u.Id
               where o.Status == OrderStatus.Cancelled && o.CancelledAt >= start && o.CancelledAt < end
               orderby o.CancelledAt descending
               select new CancellationReportDto(o.OrderNumber, o.CancelReason ?? string.Empty, o.Total, u.FullName,
                   o.CancelledAt ?? o.UpdatedAt))
            .ToListAsync(ct);

    public async Task<List<ShiftSummaryDto>> ShiftSummariesAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var shifts = await _shifts.ListAsync(start, end, ct);
        return shifts.Select(s => new ShiftSummaryDto(s.UserName, s.OpenedAt, s.ClosedAt, s.OpeningCash,
            s.CashSales, s.ExpectedCash, s.CountedCash, s.Difference, s.Status.ToString())).ToList();
    }

    public async Task<List<GrossProfitDto>> GrossProfitAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var rows = await (from oi in _db.OrderItems.AsNoTracking()
                          join o in _db.Orders.AsNoTracking() on oi.OrderId equals o.Id
                          join p in _db.Products.AsNoTracking() on oi.ProductId equals p.Id into products
                          from p in products.DefaultIfEmpty()
                          where o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end
                          select new
                          {
                              o.CompletedAt,
                              oi.LineTotal,
                              oi.Quantity,
                              Cost = p != null ? p.CostPrice : 0
                          }).ToListAsync(ct);

        return rows.GroupBy(r => r.CompletedAt!.Value.ToLocalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var revenue = g.Sum(x => x.LineTotal);
                var cost = g.Sum(x => x.Cost * x.Quantity);
                var profit = revenue - cost;
                var margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 2);
                return new GrossProfitDto(DateOnly.FromDateTime(g.Key), revenue, cost, profit, margin);
            }).ToList();
    }

    private async Task<List<Core.Entities.Order>> CompletedOrdersAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= start && o.CompletedAt < end)
            .ToListAsync(ct);

    private static (DateTimeOffset Start, DateTimeOffset End) TodayRange()
    {
        var localStart = DateTime.Today;
        var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart));
        return (start.ToUniversalTime(), start.AddDays(1).ToUniversalTime());
    }
}
