using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>A discrete capability within a module. Features are the unit scenarios are generated for.</summary>
public class Feature : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid ModuleId { get; set; }

    public RequirementModule Module { get; set; } = null!;

    public required string Name { get; set; }

    public string? Description { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>Business rules extracted for this feature, serialized as a JSON string array.</summary>
    public string? BusinessRulesJson { get; set; }

    public ICollection<UserStory> UserStories { get; set; } = new List<UserStory>();

    public ICollection<Scenario> Scenarios { get; set; } = new List<Scenario>();
}
