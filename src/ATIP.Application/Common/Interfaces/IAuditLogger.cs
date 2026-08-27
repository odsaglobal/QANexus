namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Records tenant-scoped audit events. Implementations resolve the current user + request context
/// and persist an immutable <c>AuditLogEntry</c>. Never throws to the caller — auditing must not
/// break the primary operation.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string category,
        string summary,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);
}
