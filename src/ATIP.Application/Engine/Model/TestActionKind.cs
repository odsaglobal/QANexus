namespace ATIP.Application.Engine.Model;

/// <summary>
/// The verb of a <see cref="TestAction"/>. One flat vocabulary across every platform: the engine
/// decides which driver can serve a kind, so a scenario author never has to think about wiring.
/// </summary>
/// <remarks>
/// Kinds are persisted as lower-case strings inside a step's recorded actions, so the numeric
/// values are informational. New kinds may be appended; existing names must never be renamed or a
/// stored recording stops replaying.
/// </remarks>
public enum TestActionKind
{
    // ── Navigation (web / mobile) ───────────────────────────────────────────────
    Navigate,
    Back,
    Forward,
    Reload,

    // ── Element interaction ─────────────────────────────────────────────────────
    Click,
    DoubleClick,
    RightClick,
    Hover,
    Type,
    Clear,
    Press,
    Select,
    Check,
    Uncheck,
    Upload,
    DragAndDrop,
    ScrollTo,

    // ── Waiting ─────────────────────────────────────────────────────────────────
    /// <summary>Unconditional pause. Kept because some flows genuinely need it, but a
    /// <see cref="WaitFor"/> is always preferable and the engine says so in the run log.</summary>
    Wait,

    /// <summary>Wait until a condition on an element or the page becomes true.</summary>
    WaitFor,

    // ── Windows, tabs, frames, dialogs ──────────────────────────────────────────
    SwitchTab,
    NewTab,
    CloseTab,
    SwitchFrame,

    /// <summary>Set how the next native dialog (alert/confirm/prompt) is answered.</summary>
    HandleDialog,

    // ── Browser state ───────────────────────────────────────────────────────────
    SetCookie,
    ClearCookies,
    SetStorage,
    Screenshot,

    // ── API ─────────────────────────────────────────────────────────────────────
    /// <summary>Issue an HTTP request; the response becomes the step's captured output.</summary>
    HttpRequest,

    // ── Database ────────────────────────────────────────────────────────────────
    /// <summary>Run a read query; rows become the step's captured output.</summary>
    SqlQuery,

    /// <summary>Run a write statement (insert/update/delete) for setup or teardown.</summary>
    SqlExecute,

    // ── Cross-cutting ───────────────────────────────────────────────────────────
    /// <summary>Evaluate a compiled assertion against whatever the platform exposes.</summary>
    Assert,

    /// <summary>Copy a value out of the current page/response/result set into a run variable.</summary>
    Capture,

    /// <summary>Set a run variable to a literal or interpolated value.</summary>
    SetVariable
}
