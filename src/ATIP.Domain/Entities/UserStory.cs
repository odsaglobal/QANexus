using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// A user story in the classic "As a … I want … so that …" form, with acceptance criteria,
/// extracted for a feature and used to ground scenario generation and traceability.
/// </summary>
public class UserStory : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid FeatureId { get; set; }

    public Feature Feature { get; set; } = null!;

    /// <summary>The role/persona ("As a …").</summary>
    public required string AsA { get; set; }

    /// <summary>The goal ("I want …").</summary>
    public required string IWant { get; set; }

    /// <summary>The benefit ("so that …").</summary>
    public string? SoThat { get; set; }

    /// <summary>Acceptance criteria lines, serialized as a JSON string array.</summary>
    public string? AcceptanceCriteriaJson { get; set; }
}
