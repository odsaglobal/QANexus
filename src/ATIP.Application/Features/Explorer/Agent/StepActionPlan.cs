namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>How the AI interprets a single natural-language test step into a browser action.</summary>
public sealed class StepActionPlan
{
    /// <summary>navigate | click | type | assert | none</summary>
    public string? Action { get; set; }

    /// <summary>
    /// The [ref=eN] id of the chosen element from the page snapshot. Preferred over <see cref="Target"/>
    /// for click/type: it resolves deterministically to the exact snapshotted element.
    /// </summary>
    public string? Ref { get; set; }

    /// <summary>A URL/path (for navigate) or an element hint: label, aria-label, visible text, or CSS.</summary>
    public string? Target { get; set; }

    /// <summary>The value to type (only for the "type" action).</summary>
    public string? Value { get; set; }
}
