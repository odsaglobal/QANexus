using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// An in-app notification surfaced in the tenant's notification bell (e.g. a run completed or failed).
/// Tenant-scoped; an optional <see cref="UserId"/> targets a single recipient.
/// </summary>
public class Notification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Optional recipient. When null, the notification is visible to all tenant members.</summary>
    public Guid? UserId { get; set; }

    public required string Title { get; set; }

    public required string Message { get; set; }

    /// <summary>Severity: <c>info</c>, <c>success</c>, <c>warning</c> or <c>error</c>.</summary>
    public required string Level { get; set; }

    /// <summary>Grouping category, e.g. <c>Run</c>, <c>Suite</c>, <c>System</c>.</summary>
    public required string Category { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
