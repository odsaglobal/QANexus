namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// One decision from the autonomous mission agent used by prompt-driven exploration. In addition
/// to the next browser action, the agent narrates the action as a reusable scenario step so the
/// whole flow can be recorded as a scenario once the mission completes.
/// </summary>
public sealed class MissionAgentDecision
{
    /// <summary>Short reasoning for this action.</summary>
    public string? Thought { get; set; }

    /// <summary>navigate | click | type | press | wait | finish</summary>
    public string? Action { get; set; }

    /// <summary>The [ref=eN] id of the target element from the snapshot (for click/type).</summary>
    public string? Ref { get; set; }

    /// <summary>URL/path (navigate), key name (press), or element-name fallback (click/type).</summary>
    public string? Target { get; set; }

    /// <summary>Text to type (for the "type" action).</summary>
    public string? Value { get; set; }

    /// <summary>
    /// A clean, human-readable scenario step for what this action accomplishes, phrased like a manual
    /// test step (e.g. "Enter a valid email address in the Email field"). Recorded into the scenario.
    /// </summary>
    public string? StepDescription { get; set; }

    /// <summary>The expected result after this step, if any (recorded into the scenario step).</summary>
    public string? ExpectedResult { get; set; }

    /// <summary>True when the overall mission has been accomplished and exploration should stop.</summary>
    public bool MissionComplete { get; set; }

    /// <summary>True when the agent concludes the mission cannot be completed on this app.</summary>
    public bool MissionFailed { get; set; }
}
