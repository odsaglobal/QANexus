using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>A single ordered step within a <see cref="Scenario"/>.</summary>
public class ScenarioStep : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ScenarioId { get; set; }

    public Scenario Scenario { get; set; } = null!;

    /// <summary>1-based position of the step within its scenario.</summary>
    public int Order { get; set; }

    /// <summary>The action to perform, e.g. "Enter a valid email in the Email field".</summary>
    public required string Action { get; set; }

    /// <summary>The expected outcome after the action, if any.</summary>
    public string? ExpectedResult { get; set; }

    /// <summary>
    /// JSON array of the concrete browser actions that last satisfied this step (kind, target,
    /// value). Recorded on the first successful run so subsequent runs replay deterministically
    /// instead of re-deriving actions with the AI each time. Updated in place when a locator heals.
    /// </summary>
    public string? RecordedActionsJson { get; set; }

    /// <summary>
    /// True when the step could not be executed or healed automatically and needs the user to
    /// clarify/adjust the step description before it can run reliably.
    /// </summary>
    public bool NeedsReview { get; set; }

    /// <summary>Human-readable reason the step needs review (what was missing or ambiguous).</summary>
    public string? ReviewReason { get; set; }
}
