using ATIP.Application.Engine.Model;
using ATIP.Domain.Enums;

namespace ATIP.Application.Engine.Contracts;

/// <summary>
/// Executes actions against one platform. Implemented once per system under test (Playwright for
/// web, HTTP for API, ADO.NET for database, WebDriver for mobile).
/// </summary>
/// <remarks>
/// <para>
/// A driver owns a live session — a browser page, an <c>HttpClient</c>, a pooled connection, an
/// Appium session — so it is stateful and single-threaded. The engine holds one instance per
/// platform per run and disposes them together at the end.
/// </para>
/// <para>
/// Drivers declare what they can do via <see cref="Supports"/> instead of throwing from
/// <see cref="ExecuteAsync"/>. The engine checks first, so an action a driver cannot serve is
/// reported as a configuration problem before anything is attempted, rather than surfacing as a
/// mysterious mid-run failure.
/// </para>
/// </remarks>
public interface ITestDriver : IAsyncDisposable
{
    TestPlatform Platform { get; }

    /// <summary>True if this driver implements the given verb.</summary>
    bool Supports(TestActionKind kind);

    /// <summary>
    /// Establishes the underlying session. Called once, lazily, before the first action so a
    /// scenario that never touches a platform never pays to start it.
    /// </summary>
    Task OpenAsync(RunContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Runs one action. Must not throw for ordinary failures — a missing element or a failed
    /// assertion is a result, not an exception — so that one bad step cannot abort the run.
    /// </summary>
    Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Captures whatever this platform can show after a failure: a screenshot, the response body,
    /// the rows that were returned. Never throws; returns null when there is nothing to capture.
    /// </summary>
    Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken);
}

/// <summary>Diagnostic artefact captured from a driver, typically after a failure.</summary>
public sealed record EvidenceCapture
{
    /// <summary>Base64 screenshot, when the platform has a visual surface.</summary>
    public string? ScreenshotBase64 { get; init; }

    /// <summary>Current URL, endpoint, or connection target.</summary>
    public string? Location { get; init; }

    /// <summary>Page title, response status line, or query summary.</summary>
    public string? Title { get; init; }

    /// <summary>Trimmed textual state: visible page text, response body, or result rows.</summary>
    public string? Text { get; init; }
}
