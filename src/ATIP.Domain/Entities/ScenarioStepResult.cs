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

    /// <summary>
    /// Per-assertion breakdown as JSON, when the step's expected result had been compiled into
    /// machine-checkable assertions. Lets the report show each condition with its own verdict and the
    /// concrete value observed, instead of one sentence for the whole step. Null for steps verified
    /// the older way, which still render from <see cref="Detail"/>.
    /// </summary>
    public string? ChecksJson { get; set; }

    /// <summary>URL the page was on when the step executed.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// Relative storage path (served via <c>/api/v1/files/{path}</c>) to a screenshot taken right after
    /// this step executed — visual evidence for the Executions "View" detail. Null if capture failed.
    /// </summary>
    public string? ScreenshotPath { get; set; }
}
