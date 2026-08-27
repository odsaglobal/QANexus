using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// An immutable, append-only record of a security- or data-relevant action performed by a user.
/// Powers the tenant's audit trail (who did what, when, from where).
/// </summary>
public class AuditLogEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Internal user id that performed the action (null for system/background actions).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Email of the acting user at the time of the action (denormalized for display).</summary>
    public string? UserEmail { get; set; }

    /// <summary>Machine-readable action code, e.g. <c>project.created</c>, <c>scenario.run</c>.</summary>
    public required string Action { get; set; }

    /// <summary>High-level category, e.g. <c>Project</c>, <c>Scenario</c>, <c>Suite</c>, <c>Auth</c>.</summary>
    public required string Category { get; set; }

    /// <summary>Type of the entity the action targeted, e.g. <c>Project</c>.</summary>
    public string? EntityType { get; set; }

    /// <summary>Id of the entity the action targeted, when applicable.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Human-readable summary shown in the activity log.</summary>
    public required string Summary { get; set; }

    /// <summary>Originating IP address, when available.</summary>
    public string? IpAddress { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }
}
