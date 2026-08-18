namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// One concrete, replayable browser action recorded from a successful step run. Storing the
/// action kind plus a stable, human-meaningful target (accessible role/name or URL) lets a step
/// replay deterministically and be re-located (healed) if the underlying element changes.
/// </summary>
public sealed class RecordedStepAction
{
    /// <summary>navigate | click | type | press | wait | assert</summary>
    public string Kind { get; set; } = "";

    /// <summary>Stable descriptor of the target: accessible role/name, URL/path, or key name.</summary>
    public string? Target { get; set; }

    /// <summary>Text typed (for "type") — omitted/empty for non-input actions.</summary>
    public string? Value { get; set; }

    /// <summary>True if this action's target had to be re-located (healed) on the last run.</summary>
    public bool Healed { get; set; }
}
