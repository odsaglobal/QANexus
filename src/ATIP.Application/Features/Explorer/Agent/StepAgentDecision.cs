namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// One decision from the autonomous step agent: the next browser action to take toward the
/// current test step's goal, or a signal that the step is complete/blocked. The agent reasons
/// over a live page snapshot each turn and may take several actions to satisfy a single step —
/// exactly how Copilot/Claude drive a browser through Playwright MCP.
/// </summary>
public sealed class StepAgentDecision
{
    /// <summary>Short reasoning for this action (surfaced in logs and the step detail).</summary>
    public string? Thought { get; set; }

    /// <summary>navigate | click | type | press | wait | finish</summary>
    public string? Action { get; set; }

    /// <summary>The [ref=eN] id of the target element from the snapshot (for click/type).</summary>
    public string? Ref { get; set; }

    /// <summary>URL/path (navigate), key name (press, e.g. "Enter"), or element-name fallback (click/type).</summary>
    public string? Target { get; set; }

    /// <summary>Text to type (for the "type" action).</summary>
    public string? Value { get; set; }

    /// <summary>True when the step's goal has been achieved and no further actions are needed.</summary>
    public bool StepComplete { get; set; }

    /// <summary>True when the agent concludes the step genuinely cannot be completed on this page.</summary>
    public bool StepFailed { get; set; }
}
