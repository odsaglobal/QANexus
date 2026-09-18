using ATIP.Domain.Enums;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// One concrete, replayable browser action recorded from a successful step run. Storing the
/// action kind plus a stable, human-meaningful target (accessible role/name or URL) lets a step
/// replay deterministically and be re-located (healed) if the underlying element changes.
/// </summary>
public sealed class RecordedStepAction
{
    /// <summary>
    /// navigate | click | type | press | wait | assert, plus the wider engine vocabulary
    /// (switchTab, switchFrame, handleDialog, upload, httpRequest, sqlQuery, …). Matched
    /// case-insensitively against <c>TestActionKind</c>.
    /// </summary>
    public string Kind { get; set; } = "";

    /// <summary>Stable descriptor of the target: accessible role/name, URL/path, or key name.</summary>
    public string? Target { get; set; }

    /// <summary>Text typed (for "type") — omitted/empty for non-input actions.</summary>
    public string? Value { get; set; }

    /// <summary>True if this action's target had to be re-located (healed) on the last run.</summary>
    public bool Healed { get; set; }

    /// <summary>
    /// Which system this action drives. Absent in existing recordings, which deserialize as Web —
    /// exactly what they were.
    /// </summary>
    public TestPlatform Platform { get; set; } = TestPlatform.Web;

    /// <summary>
    /// Key of the object-repository element this action binds to, when it has been promoted out of
    /// a descriptor. Steps that carry a key survive page rewording; descriptor-only steps do not.
    /// </summary>
    public string? ElementKey { get; set; }

    /// <summary>
    /// Extra arguments for kinds that need more than a target and a value — HTTP method and
    /// headers, SQL connection name, dialog answer, tab index. Null for the simple web actions,
    /// which is why older recordings round-trip unchanged.
    /// </summary>
    public Dictionary<string, string?>? Options { get; set; }

    /// <summary>
    /// For <c>Kind == "assert"</c>: the compiled, machine-checkable form of the step's expected result.
    /// Stored alongside the actions rather than in its own column so an existing recording upgrades in
    /// place — older rows simply deserialize with this null and fall back to AI verification.
    /// </summary>
    public StepAssertion? Assertion { get; set; }

    /// <summary>
    /// The expected-result text this assertion was compiled from. If the author later rewrites the
    /// expectation, the stored assertion no longer describes it, so the mismatch is detected and the
    /// assertion recompiled instead of silently checking the old requirement forever.
    /// </summary>
    public string? CompiledFrom { get; set; }
}
