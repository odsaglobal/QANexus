namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Creates in-app notifications. Callable from background contexts (no ambient user), so the tenant
/// is passed explicitly. Never throws to the caller.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(
        Guid tenantId,
        string title,
        string message,
        string level,
        string category,
        Guid? userId = null,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);
}
