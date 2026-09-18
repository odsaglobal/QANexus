using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Explorer.Agent;

namespace ATIP.Infrastructure.Engine;

/// <summary>
/// Translates a stored <see cref="RecordedStepAction"/> into the engine's <see cref="TestAction"/>.
/// </summary>
/// <remarks>
/// The recording format is the scenario's durable contract — there are rows in the database written
/// by earlier versions — so the engine does not read it directly. Keeping the translation in one
/// place means the action model can grow without every old recording needing a migration: an
/// unknown field is simply absent and the mapper supplies the old default.
/// </remarks>
internal static class RecordedActionMapper
{
    /// <summary>
    /// Builds the engine action for a recording, or null when the verb is not one the engine knows.
    /// Null rather than a best guess: the legacy replay failed unknown kinds outright, and a typo in
    /// a hand-edited recording must not turn into a step that quietly does nothing and passes.
    /// </summary>
    public static TestAction? ToTestAction(RecordedStepAction recorded, string? resolvedTarget = null)
    {
        if (!TryParseKind(recorded.Kind, out var kind))
        {
            return null;
        }

        var target = resolvedTarget ?? recorded.Target;

        var action = new TestAction
        {
            Platform = recorded.Platform,
            Kind = kind,
            Value = ValueFor(kind, recorded, target),
            Assertion = recorded.Assertion,
            CompiledFrom = recorded.CompiledFrom,
            Healed = recorded.Healed
        };

        if (recorded.Options is { Count: > 0 })
        {
            foreach (var (key, value) in recorded.Options)
            {
                action.Options[key] = value;
            }
        }

        if (NeedsElement(kind))
        {
            action.Target = new ElementTarget
            {
                ElementKey = recorded.ElementKey,
                Descriptor = target
            };
        }

        return action;
    }

    /// <summary>Maps the recorded verb onto the engine vocabulary.</summary>
    private static bool TryParseKind(string? kind, out TestActionKind parsed)
    {
        var normalized = (kind ?? string.Empty).Trim().Replace("_", string.Empty).Replace("-", string.Empty);

        parsed = normalized.ToLowerInvariant() switch
        {
            "navigate" or "goto" or "open" => TestActionKind.Navigate,
            "back" => TestActionKind.Back,
            "forward" => TestActionKind.Forward,
            "reload" or "refresh" => TestActionKind.Reload,

            "click" or "tap" => TestActionKind.Click,
            "doubleclick" or "dblclick" => TestActionKind.DoubleClick,
            "rightclick" or "contextclick" => TestActionKind.RightClick,
            "hover" => TestActionKind.Hover,
            "type" or "fill" or "enter" => TestActionKind.Type,
            "clear" => TestActionKind.Clear,
            "press" or "key" => TestActionKind.Press,
            "select" or "selectoption" => TestActionKind.Select,
            "check" => TestActionKind.Check,
            "uncheck" => TestActionKind.Uncheck,
            "upload" or "uploadfile" => TestActionKind.Upload,
            "draganddrop" or "drag" => TestActionKind.DragAndDrop,
            "scrollto" or "scroll" => TestActionKind.ScrollTo,

            "wait" => TestActionKind.Wait,
            "waitfor" => TestActionKind.WaitFor,

            "switchtab" or "selecttab" => TestActionKind.SwitchTab,
            "newtab" or "opentab" => TestActionKind.NewTab,
            "closetab" => TestActionKind.CloseTab,
            "switchframe" or "selectframe" or "frame" => TestActionKind.SwitchFrame,
            "handledialog" or "alert" or "acceptalert" or "dismissalert" => TestActionKind.HandleDialog,

            "setcookie" => TestActionKind.SetCookie,
            "clearcookies" => TestActionKind.ClearCookies,
            "setstorage" => TestActionKind.SetStorage,
            "screenshot" => TestActionKind.Screenshot,

            "httprequest" or "apirequest" or "request" => TestActionKind.HttpRequest,
            "sqlquery" or "dbquery" or "query" => TestActionKind.SqlQuery,
            "sqlexecute" or "dbexecute" => TestActionKind.SqlExecute,

            "capture" or "store" or "save" => TestActionKind.Capture,
            "setvariable" or "set" => TestActionKind.SetVariable,
            "assert" or "verify" or "expect" => TestActionKind.Assert,

            _ => (TestActionKind)(-1)
        };

        return Enum.IsDefined(parsed);
    }

    /// <summary>
    /// Whether a recorded verb acts on an element, and so could be repaired by re-locating it.
    /// An unrecognised verb is not element-based: it never reaches a driver, so a locator is not
    /// what is wrong with it.
    /// </summary>
    public static bool TargetsElement(string? kind) =>
        TryParseKind(kind, out var parsed) && NeedsElement(parsed);

    /// <summary>
    /// Kinds whose verbs act on a page element. The rest address the session, the network or a
    /// database, and giving them a target would make a missing descriptor look like a failure.
    /// </summary>
    private static bool NeedsElement(TestActionKind kind) => kind switch
    {
        TestActionKind.Click or TestActionKind.DoubleClick or TestActionKind.RightClick
            or TestActionKind.Hover or TestActionKind.Type or TestActionKind.Clear
            or TestActionKind.Select or TestActionKind.Check or TestActionKind.Uncheck
            or TestActionKind.Upload or TestActionKind.DragAndDrop or TestActionKind.ScrollTo
            or TestActionKind.WaitFor => true,

        // Press is element-scoped when a field is named and page-level otherwise; the driver's
        // element path is used only when a descriptor exists.
        TestActionKind.Press => false,

        _ => false
    };

    /// <summary>
    /// Recovers the action's payload. The recorded format overloads <c>Target</c>: for a navigate
    /// it holds the URL and for a press it holds the key, while <c>Value</c> is only used for typed
    /// text. Normalising that here keeps the drivers free of the legacy shape.
    /// </summary>
    private static string? ValueFor(TestActionKind kind, RecordedStepAction recorded, string? target) => kind switch
    {
        TestActionKind.Navigate => string.IsNullOrWhiteSpace(recorded.Value) ? target : recorded.Value,
        TestActionKind.Press => string.IsNullOrWhiteSpace(recorded.Value) ? target : recorded.Value,
        _ => recorded.Value
    };
}
