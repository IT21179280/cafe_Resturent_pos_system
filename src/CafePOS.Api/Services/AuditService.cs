using System.Text.Json;
using CafePOS.Api.Data;
using CafePOS.Core.Entities;

namespace CafePOS.Api.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    private readonly Security.CurrentUser _currentUser;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, Security.CurrentUser currentUser, ILogger<AuditService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityName, string? entityId, object? details = null,
        CancellationToken ct = default)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId,
                UserName = _currentUser.Name,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details is null ? null : JsonSerializer.Serialize(details),
                IpAddress = _currentUser.IpAddress
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Auditing must never break the business operation, but it must be visible in logs.
            _logger.LogError(ex, "Failed to write audit log for {Action} on {Entity}", action, entityName);
        }
    }
}
