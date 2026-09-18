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

    // The tab list is STATE, not history: only the latest matters, so it is held separately rather than
    // consuming slots in the (bounded) event buffer, where repeated updates would evict real activity.
    private readonly ConcurrentDictionary<Guid, object> _lastTabs = new();

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
            _lastTabs.TryRemove(sessionId, out _);
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

    public Task PublishScenarioAsync(Guid tenantId, Guid sessionId, ScenarioProgressLiveUpdate scenario, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sessionId,
            scenario.ScenarioId,
            scenario.ScenarioTitle,
            scenario.Index,
            scenario.Total,
            scenario.StepCount,
            scenario.Status,
        };
        // Buffered like steps and logs: a viewer who opens the dialog mid-suite must still learn which
        // tests already ran and how they ended, not just the one currently executing.
        Buffer(sessionId, "scenario", payload);
        return _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("scenario", payload, cancellationToken);
    }

    public Task PublishLogAsync(Guid tenantId, Guid sessionId, ExplorationLogEntry log, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sessionId,
            log.Id,
            log.Level,
            log.Message,
            log.TimestampUtc,
        };
        Buffer(sessionId, "log", payload);
        return _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("log", payload, cancellationToken);
    }

    public Task PublishTabsAsync(Guid tenantId, Guid sessionId, IReadOnlyList<BrowserTabInfo> tabs, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sessionId,
            tabs = tabs.Select(t => new { t.Index, t.Title, t.Url, t.IsActive }).ToList(),
        };
        _lastTabs[sessionId] = payload;
        return _hub.Clients
            .Group(ExplorationHub.GroupName(tenantId, sessionId))
            .SendAsync("tabs", payload, cancellationToken);
    }

    public async Task ReplayRecentAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken = default)
    {
        // Tabs first: it is a standalone snapshot, so a late joiner gets the correct tab strip even when
        // no log/step events remain buffered.
        if (_lastTabs.TryGetValue(sessionId, out var tabs))
        {
            await _hub.Clients.Client(connectionId).SendAsync("tabs", tabs, cancellationToken);
        }

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
