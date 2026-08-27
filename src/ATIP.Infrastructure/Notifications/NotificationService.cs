using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Notifications;

/// <summary>
/// Persists in-app notifications in their own DB scope so they work from background contexts and
/// never fail the primary operation.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock,
        ILogger<NotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task NotifyAsync(
        Guid tenantId,
        string title,
        string message,
        string level,
        string category,
        Guid? userId = null,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Title = title,
                Message = message,
                Level = level,
                Category = category,
                EntityType = entityType,
                EntityId = entityId,
                CreatedAtUtc = _clock.UtcNow,
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification '{Title}'", title);
        }
    }
}
