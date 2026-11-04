using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafePOS.Api.Data;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

/// <summary>
/// Mirrors the restaurant's operational snapshot to a Firebase Realtime Database.
/// PostgreSQL remains the source of truth; Firebase is a read-only copy for admins.
/// </summary>
public sealed class FirebaseSyncService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FirebaseSyncService> _logger;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public FirebaseSyncService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory,
        ILogger<FirebaseSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so migrations/seeding finish before the first mirror.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncIfEnabledAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Firebase background sync failure.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Runs a sync immediately, for the Settings "Sync now" button.</summary>
    public async Task<FirebaseSyncResult> SyncNowAsync(CancellationToken ct = default)
    {
        await _syncGate.WaitAsync(ct);
        string? authToken = null;
        try
        {
            using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var all = await settings.GetAllAsync(ct);

            var enabled = bool.TryParse(all.GetValueOrDefault(SettingKeys.FirebaseSyncEnabled), out var flag) && flag;
            var databaseUrl = all.GetValueOrDefault(SettingKeys.FirebaseDatabaseUrl, string.Empty).Trim();
            if (!enabled || string.IsNullOrWhiteSpace(databaseUrl))
            {
                return new FirebaseSyncResult(false, "Firebase sync is not enabled or configured.", DateTimeOffset.UtcNow);
            }

            var snapshot = await BuildSnapshotAsync(db, all, ct);
            authToken = all.GetValueOrDefault(SettingKeys.FirebaseAuthToken);
            var uri = BuildUri(databaseUrl, authToken);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var client = _httpClientFactory.CreateClient(nameof(FirebaseSyncService));
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PutAsync(uri, content, timeout.Token);
            var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var detail = string.IsNullOrWhiteSpace(responseBody)
                    ? response.ReasonPhrase
                    : responseBody.Length > 500 ? responseBody[..500] : responseBody;
                detail = Redact(detail, authToken);
                var message = $"Firebase returned {(int)response.StatusCode}: {detail}";
                await StoreStatusAsync(settings, null, message);
                _logger.LogWarning("Firebase sync failed: {Message}", message);
                return new FirebaseSyncResult(false, message, DateTimeOffset.UtcNow);
            }

            var completedAt = DateTimeOffset.UtcNow;
            await StoreStatusAsync(settings, completedAt, string.Empty);
            _logger.LogInformation("Firebase sync completed at {CompletedAt}.", completedAt);
            return new FirebaseSyncResult(true, "POS data was synced to Firebase.", completedAt);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            const string message = "Firebase sync timed out. Check the database URL and network connection.";
            await TryStoreFailureAsync(message);
            _logger.LogWarning(message);
            return new FirebaseSyncResult(false, message, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            const string message = "Firebase sync failed. Check the database URL and network connection.";
            await TryStoreFailureAsync(message);
            _logger.LogWarning("Firebase sync failed: {Type}: {Detail}", ex.GetType().Name,
                Redact(ex.Message, authToken));
            return new FirebaseSyncResult(false, message, DateTimeOffset.UtcNow);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task SyncIfEnabledAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
        var all = await settings.GetAllAsync(ct);
        var enabled = bool.TryParse(all.GetValueOrDefault(SettingKeys.FirebaseSyncEnabled), out var flag) && flag;
        if (!enabled || string.IsNullOrWhiteSpace(all.GetValueOrDefault(SettingKeys.FirebaseDatabaseUrl)))
            return;

        await SyncNowAsync(ct);
    }

    private async Task TryStoreFailureAsync(string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            await StoreStatusAsync(settings, null, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not store the Firebase sync error.");
        }
    }

    private static async Task StoreStatusAsync(SettingsService settings, DateTimeOffset? syncedAt, string error)
    {
        if (syncedAt.HasValue)
            await settings.SetAsync(SettingKeys.FirebaseLastSyncedAt,
                syncedAt.Value.ToString("O", CultureInfo.InvariantCulture), "Last successful Firebase sync");
        await settings.SetAsync(SettingKeys.FirebaseLastError, error, "Last Firebase sync error");
    }

    private static Uri BuildUri(string databaseUrl, string? token)
    {
        var input = new Uri(databaseUrl, UriKind.Absolute);
        var path = input.AbsolutePath.TrimEnd('/');
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path = path[..^5];
        path += "/cafepos.json";

        var query = input.Query.TrimStart('?');
        if (!string.IsNullOrWhiteSpace(token))
        {
            var auth = "auth=" + Uri.EscapeDataString(token);
            query = string.IsNullOrWhiteSpace(query) ? auth : query + "&" + auth;
        }

        return new UriBuilder(input) { Path = path, Query = query }.Uri;
    }

    private static async Task<object> BuildSnapshotAsync(AppDbContext db, Dictionary<string, string> settings,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var (todayStart, todayEnd) = TodayRange();
        var dailyStart = todayStart.AddDays(-59);

        var todayOrders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= todayStart && o.CompletedAt < todayEnd)
            .Select(o => new { o.Total, o.DiscountAmount, o.TaxAmount, o.ServiceCharge })
            .ToListAsync(ct);

        var paymentRows = await (from p in db.Payments.AsNoTracking()
                                 join o in db.Orders.AsNoTracking() on p.OrderId equals o.Id
                                 where o.Status == OrderStatus.Completed && o.CompletedAt >= todayStart && o.CompletedAt < todayEnd
                                 select new { p.Method, p.Amount }).ToListAsync(ct);

        var dailyRows = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= dailyStart && o.CompletedAt < todayEnd)
            .Select(o => new { o.CompletedAt, o.Total, o.DiscountAmount, o.TaxAmount, o.ServiceCharge })
            .ToListAsync(ct);

        var categories = await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Description, c.DisplayOrder, c.IsActive, c.ImageUrl })
            .ToListAsync(ct);

        var products = await db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Sku, p.Barcode, p.CategoryId,
                Category = p.Category != null ? p.Category.Name : null,
                p.Description, p.Price, p.CostPrice, p.ImageUrl, p.IsAvailable,
                PreparationStation = p.PreparationStation.ToString(), p.TaxRate, p.TrackInventory
            })
            .ToListAsync(ct);

        var recentOrders = await db.Orders.AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .OrderByDescending(o => o.PlacedAt)
            .Take(200)
            .ToListAsync(ct);

        var ingredients = await db.Ingredients.AsNoTracking()
            .OrderBy(i => i.Name)
            .ToListAsync(ct);

        var tables = await db.DiningTables.AsNoTracking()
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Number)
            .Select(t => new { t.Id, t.Number, t.Capacity, t.Area, Status = t.Status.ToString(), t.CurrentOrderId })
            .ToListAsync(ct);

        var pendingKitchen = await db.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing, ct);

        var todaySales = todayOrders.Sum(o => o.Total);
        var todayCount = todayOrders.Count;
        var average = todayCount == 0
            ? 0
            : Math.Round(todaySales / todayCount, 2, MidpointRounding.AwayFromZero);

        var lowStock = ingredients
            .Where(i => i.CurrentStock <= i.LowStockThreshold)
            .OrderBy(i => i.CurrentStock - i.LowStockThreshold)
            .ToList();

        var snapshot = new
        {
            schemaVersion = 1,
            generatedAt = now,
            timeZone = TimeZoneInfo.Local.Id,
            restaurant = new
            {
                name = Value(settings, SettingKeys.RestaurantName, "CafePOS Restaurant"),
                address = Value(settings, SettingKeys.RestaurantAddress),
                phone = Value(settings, SettingKeys.RestaurantPhone),
                email = Value(settings, SettingKeys.RestaurantEmail),
                currency = Value(settings, SettingKeys.Currency, "LKR"),
                systemName = Value(settings, SettingKeys.SystemName, "CafePOS"),
                logoUrl = Value(settings, SettingKeys.LogoUrl)
            },
            summary = new
            {
                date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                todaySales,
                orderCount = todayCount,
                averageOrderValue = average,
                tax = todayOrders.Sum(o => o.TaxAmount),
                serviceCharge = todayOrders.Sum(o => o.ServiceCharge),
                discounts = todayOrders.Sum(o => o.DiscountAmount),
                openTables = tables.Count(t => t.Status == TableStatus.Occupied.ToString()),
                pendingKitchen,
                lowStockCount = lowStock.Count,
                paymentMethods = paymentRows
                    .GroupBy(p => p.Method)
                    .Select(g => new { method = g.Key.ToString(), count = g.Count(), amount = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.amount)
                    .ToList()
            },
            categories = categories.ToDictionary(c => c.Id.ToString(), c => new
            {
                c.Id, c.Name, c.Description, c.DisplayOrder, c.IsActive, c.ImageUrl
            }),
            products = products.ToDictionary(p => p.Id.ToString(), p => new
            {
                p.Id, p.Name, p.Sku, p.Barcode, p.CategoryId, p.Category, p.Description, p.Price, p.CostPrice,
                p.ImageUrl, p.IsAvailable, p.PreparationStation, p.TaxRate, p.TrackInventory
            }),
            recentOrders = recentOrders.ToDictionary(o => o.Id.ToString(), o => new
            {
                o.Id, o.OrderNumber, type = o.Type.ToString(), status = o.Status.ToString(),
                table = o.Table?.Number, o.CustomerName, o.CustomerPhone, o.DeliveryAddress, o.Notes,
                o.Subtotal, o.DiscountAmount, o.TaxAmount, o.ServiceCharge, o.Total, o.PaidAmount, o.ChangeDue,
                o.PlacedAt, o.CompletedAt, o.CancelledAt,
                items = o.Items.Select(i => new
                {
                    i.Id, i.ProductName, i.UnitPrice, i.Quantity, i.DiscountAmount, i.TaxAmount, i.LineTotal,
                    station = i.PreparationStation.ToString(), i.Notes
                }).ToList()
            }),
            dailySales = dailyRows
                .GroupBy(o => o.CompletedAt!.Value.ToLocalTime().Date)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    g => new
                    {
                        orders = g.Count(),
                        sales = g.Sum(o => o.Total),
                        tax = g.Sum(o => o.TaxAmount),
                        serviceCharge = g.Sum(o => o.ServiceCharge),
                        discounts = g.Sum(o => o.DiscountAmount)
                    }),
            inventory = ingredients.ToDictionary(i => i.Id.ToString(), i => new
            {
                i.Id, i.Name, i.Sku, i.Unit, i.CurrentStock, i.LowStockThreshold, i.CostPerUnit,
                lowStock = i.CurrentStock <= i.LowStockThreshold
            }),
            lowStock = lowStock.ToDictionary(i => i.Id.ToString(), i => new
            {
                i.Id, i.Name, i.Unit, i.CurrentStock, i.LowStockThreshold,
                shortage = Math.Max(0, i.LowStockThreshold - i.CurrentStock)
            }),
            tables = tables.ToDictionary(t => t.Id.ToString(), t => new
            {
                t.Id, t.Number, t.Capacity, t.Area, t.Status, t.CurrentOrderId
            })
        };

        return snapshot;
    }

    private static string Value(IReadOnlyDictionary<string, string> settings, string key, string fallback = "") =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string Redact(string? value, string? secret) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.IsNullOrWhiteSpace(secret)
                ? value
                : value.Replace(secret, "***", StringComparison.Ordinal);

    private static (DateTimeOffset Start, DateTimeOffset End) TodayRange()
    {
        var localStart = DateTime.Today;
        var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart));
        return (start.ToUniversalTime(), start.AddDays(1).ToUniversalTime());
    }
}
