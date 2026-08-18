using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A unit of work in the platform. A project owns requirements, environments, discovered
/// application knowledge, scenarios and executions. It is the primary permission boundary.
/// </summary>
public class Project : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public required string Name { get; set; }

    /// <summary>Unique (per tenant) URL-safe key, e.g. "checkout-web".</summary>
    public required string Key { get; set; }

    public string? Description { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<Environment> Environments { get; set; } = new List<Environment>();

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

    public ICollection<Credential> Credentials { get; set; } = new List<Credential>();

    public ICollection<TestDataSet> TestDataSets { get; set; } = new List<TestDataSet>();
}
