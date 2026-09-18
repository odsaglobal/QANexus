using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;

namespace ATIP.Application.Engine.Model;

/// <summary>
/// The single unit of work the engine executes: one verb, on one platform, against one target.
/// </summary>
/// <remarks>
/// <para>
/// A scenario step compiles down to an ordered list of these. The type is deliberately flat and
/// serializable — it is persisted verbatim inside <c>ScenarioStep.RecordedActionsJson</c>, so a
/// recorded run is a portable script that can be re-executed with no AI involvement at all.
/// </para>
/// <para>
/// Platform-specific arguments live in <see cref="Options"/> rather than in subclasses. Subclasses
/// would force every consumer (JSON round-trip, editor UI, exporter) to know the full type
/// hierarchy, and the set of options is exactly the thing that grows fastest.
/// </para>
/// </remarks>
public sealed class TestAction
{
    public TestPlatform Platform { get; set; } = TestPlatform.Web;

    public TestActionKind Kind { get; set; } = TestActionKind.Click;

    /// <summary>What to act on. Null for actions that need no element (navigate, wait, SQL, HTTP).</summary>
    public ElementTarget? Target { get; set; }

    /// <summary>
    /// The action's payload: text to type, URL to open, key to press, option to select, SQL text,
    /// request body. May contain <c>{{variable}}</c> tokens, resolved immediately before execution.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Extra arguments keyed by name — HTTP method/headers, dialog answer, frame selector, tab
    /// index, timeout override, connection name. See <see cref="ActionOptionNames"/>.
    /// </summary>
    public Dictionary<string, string?> Options { get; set; } = [];

    /// <summary>For <see cref="TestActionKind.Assert"/>: the compiled, machine-checkable condition.</summary>
    public StepAssertion? Assertion { get; set; }

    /// <summary>
    /// When set, the action's output is stored as a run variable under this name, usable by later
    /// steps as <c>{{name}}</c>. This is what links the platforms together — capture the order id
    /// from the API response, then assert on it in the database.
    /// </summary>
    public string? SaveAs { get; set; }

    /// <summary>True if this action's locator had to be healed on the last run.</summary>
    public bool Healed { get; set; }

    /// <summary>
    /// The expected-result text an <see cref="Assertion"/> was compiled from. A mismatch against
    /// the step's current expectation means the assertion is stale and must be recompiled.
    /// </summary>
    public string? CompiledFrom { get; set; }

    public string? Option(string name) =>
        Options.TryGetValue(name, out var value) ? value : null;

    public int OptionAsInt(string name, int fallback) =>
        int.TryParse(Option(name), out var parsed) ? parsed : fallback;

    public bool OptionAsBool(string name, bool fallback) =>
        bool.TryParse(Option(name), out var parsed) ? parsed : fallback;

    public override string ToString() =>
        Target is null || Target.IsEmpty
            ? $"{Platform}/{Kind}"
            : $"{Platform}/{Kind} → {Target}";
}

/// <summary>
/// Well-known <see cref="TestAction.Options"/> keys. Constants rather than magic strings so a
/// driver and the recorder that feeds it cannot drift apart silently.
/// </summary>
public static class ActionOptionNames
{
    // Shared
    public const string TimeoutMs = "timeoutMs";

    // Web / mobile
    public const string Condition = "condition";          // visible | hidden | enabled | text | url
    public const string FrameSelector = "frame";           // CSS/name of the iframe, or "parent" / "top"
    public const string TabIndex = "tabIndex";
    public const string TabUrlContains = "tabUrlContains";
    public const string TabTitleContains = "tabTitleContains";
    public const string DialogAction = "dialogAction";     // accept | dismiss
    public const string DialogPromptText = "dialogPromptText";
    public const string FilePaths = "filePaths";           // newline separated
    public const string CookieName = "cookieName";
    public const string CookieDomain = "cookieDomain";
    public const string StorageKind = "storageKind";       // local | session
    public const string StorageKey = "storageKey";
    public const string DropTarget = "dropTarget";         // ElementTarget descriptor for DragAndDrop

    // API
    public const string HttpMethod = "method";
    public const string HttpUrl = "url";
    public const string HttpHeaders = "headers";           // JSON object
    public const string HttpQuery = "query";               // JSON object
    public const string HttpContentType = "contentType";
    public const string ExpectedStatus = "expectedStatus";

    // Database
    public const string ConnectionName = "connection";
    public const string SqlParameters = "parameters";      // JSON object

    // Capture
    public const string CaptureSource = "captureSource";   // text | value | attribute | url | title | json | column
    public const string CaptureExpression = "captureExpression"; // attribute name, JSON path, or column name
}
