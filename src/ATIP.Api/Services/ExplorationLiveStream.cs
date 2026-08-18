using System.Collections.Concurrent;
using ATIP.Api.Hubs;
using ATIP.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ATIP.Api.Services;

/// <summary>
/// <see cref="IExplorationLiveStream"/> backed by SignalR. Every frame and status update is
/// delivered only to the group that matches the owning tenant and session, guaranteeing
/// cross-organization isolation. A small in-memory ring buffer of recent log/step events per
/// session lets clients that connect mid-run replay prior activity instead of an empty log.
/// </summary>
public sealed class ExplorationLiveStream : IExplorationLiveStream
{
    private const int MaxBufferedPerSession = 400;

    private readonly IHubContext<ExplorationHub> _hub;

    // Recent replayable events (log + step) keyed by session, for clients that join mid-run.
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<(string Event, object Payload)>> _recent = new();

    public ExplorationLiveStream(IHubContext<ExplorationHub> hub) => _hub = hub;

    public Task PublishFrameAsync(Guid tenantId, Guid sessionId, string base64Jpeg, CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("frame", new { sessionId, data = base64Jpeg }, cancellationToken);

    public Task PublishStatusAsync(Guid tenantId, Guid sessionId, ExplorationLiveStatus status, CancellationToken cancellationToken = default)
    {
        // Free the replay buffer once a session reaches a terminal state to bound memory.
        if (status.Status is "Completed" or "Failed" or "Cancelled")
        {
            _recent.TryRemove(sessionId, out _);
        }

        return _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("status", new
            {
                sessionId,
                status.Status,
                status.CurrentUrl,
                status.PagesDiscovered,
                status.ElementsDiscovered,
            }, cancellationToken);
    }

    public Task PublishStepAsync(Guid tenantId, Guid sessionId, ScenarioStepLiveUpdate step, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sessionId,
            step.ScenarioId,
            step.ScenarioTitle,
            step.StepOrder,
            step.Action,
            step.Status,
            step.Detail,
            step.Url,
        };
        Buffer(sessionId, "step", payload);
        return _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("step", payload, cancellationToken);
    }

    public Task PublishLogAsync(Guid tenantId, Guid sessionId, ExplorationLogEntry log, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sessionId,
            log.Level,
            log.Message,
            log.TimestampUtc,
        };
        Buffer(sessionId, "log", payload);
        return _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("log", payload, cancellationToken);
    }

    public async Task ReplayRecentAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken = default)
    {
        if (!_recent.TryGetValue(sessionId, out var queue))
        {
            return;
        }

        foreach (var (ev, payload) in queue.ToArray())
        {
            await _hub.Clients.Client(connectionId).SendAsync(ev, payload, cancellationToken);
        }
    }

    private void Buffer(Guid sessionId, string ev, object payload)
    {
        var queue = _recent.GetOrAdd(sessionId, _ => new ConcurrentQueue<(string, object)>());
        queue.Enqueue((ev, payload));
        while (queue.Count > MaxBufferedPerSession && queue.TryDequeue(out _))
        {
            // Trim to the most recent events.
        }
    }
}
