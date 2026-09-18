using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A generated (or manually authored) test scenario for a feature. Steps are stored separately
/// and ordered; execution reuses this stored knowledge rather than regenerating scenarios.
/// </summary>
public class Scenario : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid FeatureId { get; set; }

    public Feature Feature { get; set; } = null!;

    public required string Title { get; set; }

    public ScenarioType Type { get; set; } = ScenarioType.Positive;

    public Priority Priority { get; set; } = Priority.Medium;

    public RiskLevel Risk { get; set; } = RiskLevel.Low;

    public ScenarioSource Source { get; set; } = ScenarioSource.AiGenerated;

    public string? Preconditions { get; set; }

    public string? ExpectedResult { get; set; }

    /// <summary>Optional Jira issue key this scenario traces to (e.g. PROJ-123).</summary>
    public string? JiraKey { get; set; }

    /// <summary>Free-form tags, serialized as a JSON string array.</summary>
    public string? TagsJson { get; set; }

    /// <summary>
    /// AI-discovered steps from a goal-driven exploration, awaiting the user's confirmation. When set,
    /// the UI offers to replace the current steps with these (grounded in the real app).
    /// Serialized as a JSON array of { order, action, expectedResult }.
    /// </summary>
    public string? ProposedStepsJson { get; set; }

    /// <summary>
    /// Snapshot of the steps that were in effect before proposed steps were applied, so the user can
    /// revert. Serialized the same way as <see cref="ProposedStepsJson"/>.
    /// </summary>
    public string? PreviousStepsJson { get; set; }

    /// <summary>
    /// When true (default), a RUN that cannot replay a recorded locator — or that has no recording yet —
    /// may fall back to one focused AI turn to re-locate the element and update the recording
    /// ("self-healing"). When false the run stays 100% deterministic: a missing/broken locator fails the
    /// step immediately and no LLM call is ever made. Runs that replay cleanly never call the AI either way.
    /// </summary>
    public bool AutoHealEnabled { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<ScenarioStep> Steps { get; set; } = new List<ScenarioStep>();
}
