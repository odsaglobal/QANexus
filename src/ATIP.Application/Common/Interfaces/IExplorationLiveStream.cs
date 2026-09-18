namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Publishes live exploration updates — screencast frames and progress — to connected
/// clients. All updates are scoped by <c>tenantId</c> and <c>sessionId</c> so that
/// organizations only ever receive frames for their own exploration sessions.
/// </summary>
public interface IExplorationLiveStream
{
    /// <summary>Pushes a single base64-encoded JPEG frame to the session's tenant group.</summary>
    Task PublishFrameAsync(Guid tenantId, Guid sessionId, string base64Jpeg, CancellationToken cancellationToken = default);

    /// <summary>Pushes a progress/status update to the session's tenant group.</summary>
    Task PublishStatusAsync(Guid tenantId, Guid sessionId, ExplorationLiveStatus status, CancellationToken cancellationToken = default);

    /// <summary>Pushes a single scenario-step execution result to the session's tenant group as it happens.</summary>
    Task PublishStepAsync(Guid tenantId, Guid sessionId, ScenarioStepLiveUpdate step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the progress of the run through its scenarios: which test has just started, its position
    /// in the suite, and — once it finishes — its rolled-up verdict.
    /// </summary>
    Task PublishScenarioAsync(Guid tenantId, Guid sessionId, ScenarioProgressLiveUpdate scenario, CancellationToken cancellationToken = default);

    /// <summary>Pushes a human-readable activity log line to the session's tenant group as exploration progresses.</summary>
    Task PublishLogAsync(Guid tenantId, Guid sessionId, ExplorationLogEntry log, CancellationToken cancellationToken = default);

    /// <summary>Pushes the browser's current tab list so viewers can see popups the site opened, and which one is in view.</summary>
    Task PublishTabsAsync(Guid tenantId, Guid sessionId, IReadOnlyList<BrowserTabInfo> tabs, CancellationToken cancellationToken = default);

    /// <summary>Replays recently buffered log/step events to a single just-connected client so late joiners see prior activity.</summary>
    Task ReplayRecentAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken = default);
}

/// <summary>One open browser tab, as reported by the automation driver.</summary>
public sealed record BrowserTabInfo(int Index, string Title, string Url, bool IsActive);

/// <summary>Snapshot of live exploration progress broadcast alongside screencast frames.</summary>
public sealed record ExplorationLiveStatus(
    string Status,
    string? CurrentUrl,
    int PagesDiscovered,
    int ElementsDiscovered);

/// <summary>A single scenario step's live execution result, streamed during a scenario or suite run.</summary>
public sealed record ScenarioStepLiveUpdate(
    Guid ScenarioId,
    string? ScenarioTitle,
    int StepOrder,
    string Action,
    string Status,
    string? Detail,
    string? Url);

/// <summary>
/// One test's progress through a run. A suite executes its scenarios sequentially inside a single
/// session, so without this the viewer can only infer which test is live by watching step numbers
/// restart. Emitted once as <c>Running</c> when the scenario starts and again with its verdict.
/// </summary>
/// <param name="Index">1-based position of this scenario within the run.</param>
/// <param name="Total">How many scenarios the run will execute in total.</param>
/// <param name="StepCount">Steps this scenario contains, so the viewer can show "step 2 of 4".</param>
/// <param name="Status">
/// <c>Running</c> while executing, then <c>Passed</c>, <c>Healed</c>, <c>Failed</c> or <c>Skipped</c> —
/// the worst outcome among the scenario's steps, which is the verdict a tester cares about.
/// </param>
public sealed record ScenarioProgressLiveUpdate(
    Guid ScenarioId,
    string ScenarioTitle,
    int Index,
    int Total,
    int StepCount,
    string Status);

/// <summary>A single human-readable activity log line streamed while an exploration is running.</summary>
/// <param name="Id">
/// Optional stable correlation id. Lines that carry one REPLACE any earlier line with the same id
/// instead of appending, which lets a step be announced the instant it starts and then be rewritten
/// with its verdict seconds later — so the log tracks the live browser rather than trailing it.
/// </param>
public sealed record ExplorationLogEntry(
    string Level,
    string Message,
    DateTimeOffset TimestampUtc,
    string? Id = null);
