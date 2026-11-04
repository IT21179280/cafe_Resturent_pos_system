using CafePOS.Api.Data;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

public class SettingsService
{
    private readonly AppDbContext _db;

    public SettingsService(AppDbContext db) => _db = db;

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
        await _db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public async Task<string> GetAsync(string key, string fallback, CancellationToken ct = default)
    {
        var value = await _db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, string.Empty, ct);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task SetAsync(string key, string value, string? description, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value, Description = description });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(description)) setting.Description = description;
        }
        await _db.SaveChangesAsync(ct);
    }
}
