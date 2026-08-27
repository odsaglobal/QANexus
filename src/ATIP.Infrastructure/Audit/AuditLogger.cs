using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Audit;

/// <summary>
/// Writes audit entries in their own DB scope so the trail persists independently of the caller's
/// unit of work, and never throws back into the primary operation.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        IServiceScopeFactory scopeFactory,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        ILogger<AuditLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string category,
        string summary,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUser.TenantId;
            if (tenantId is null)
            {
                return; // No tenant context (anonymous/system) — nothing to scope the entry to.
            }

            var entry = new AuditLogEntry
            {
                TenantId = tenantId.Value,
                UserId = _currentUser.UserId,
                UserEmail = _currentUser.Email,
                Action = action,
                Category = category,
                Summary = summary,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = _currentUser.IpAddress,
                TimestampUtc = _clock.UtcNow,
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit log entry {Action}", action);
        }
    }
}
