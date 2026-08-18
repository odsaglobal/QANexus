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

    /// <summary>Pushes a human-readable activity log line to the session's tenant group as exploration progresses.</summary>
    Task PublishLogAsync(Guid tenantId, Guid sessionId, ExplorationLogEntry log, CancellationToken cancellationToken = default);

    /// <summary>Replays recently buffered log/step events to a single just-connected client so late joiners see prior activity.</summary>
    Task ReplayRecentAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken = default);
}

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

/// <summary>A single human-readable activity log line streamed while an exploration is running.</summary>
public sealed record ExplorationLogEntry(
    string Level,
    string Message,
    DateTimeOffset TimestampUtc);
