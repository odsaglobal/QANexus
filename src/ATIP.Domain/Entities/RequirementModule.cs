using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>A functional area of the application identified by AI analysis of a requirement.</summary>
public class RequirementModule : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid RequirementId { get; set; }

    public Requirement Requirement { get; set; } = null!;

    public required string Name { get; set; }

    public string? Description { get; set; }

    public ICollection<Feature> Features { get; set; } = new List<Feature>();
}
