using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// Result of executing one step of a scenario during a scenario-scoped exploration run.
/// Captures what the agent did, whether it passed, healed a locator, or failed.
/// </summary>
public class ScenarioStepResult : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>The exploration session this run belongs to.</summary>
    public Guid SessionId { get; set; }

    public Guid ScenarioId { get; set; }

    /// <summary>1-based order of the step within the scenario.</summary>
    public int StepOrder { get; set; }

    public required string Action { get; set; }

    public StepRunStatus Status { get; set; } = StepRunStatus.Skipped;

    /// <summary>Human-readable detail of what happened (interpreted action, healing, or failure reason).</summary>
    public string? Detail { get; set; }

    /// <summary>URL the page was on when the step executed.</summary>
    public string? Url { get; set; }
}
