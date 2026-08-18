using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// One locator strategy and value for a <see cref="DiscoveredElement"/>. Storing multiple
/// strategies per element is the foundation of the self-healing engine: if the primary
/// locator fails, the engine tries alternates in confidence order.
/// </summary>
public class ElementLocator : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ElementId { get; set; }

    public DiscoveredElement Element { get; set; } = null!;

    public LocatorStrategy Strategy { get; set; }

    public required string Value { get; set; }

    public bool IsPrimary { get; set; }

    public double ConfidenceScore { get; set; } = 1.0;

    public bool IsVerified { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }

    /// <summary>Number of consecutive execution failures using this locator.</summary>
    public int FailureCount { get; set; }
}
