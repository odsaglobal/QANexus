using System.Collections.Concurrent;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Long-running service that processes exploration sessions from the in-memory queue with
/// bounded concurrency, so multiple users' runs execute independently and in parallel. Each
/// session is processed in its own DI scope (fresh DbContext, ExplorerAgent, and — for MCP runs —
/// its own isolated Playwright MCP server process).
/// </summary>
public sealed class ExplorationBackgroundService : BackgroundService
{
    private readonly ExplorationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _maxParallel;
    private readonly ILogger<ExplorationBackgroundService> _logger;

    public ExplorationBackgroundService(
        ExplorationQueue queue,
        IServiceScopeFactory scopeFactory,
        int maxParallelSessions,
        ILogger<ExplorationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _maxParallel = Math.Max(1, maxParallelSessions);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Exploration background service started (max {Max} parallel session(s)).", _maxParallel);

        // Durable recovery: the DB is the source of truth. Sessions persisted as Pending before a
        // restart are lost from the in-memory channel, and sessions left Running were interrupted
        // mid-flight. Reconcile both so runs survive an API restart.
        await RecoverInterruptedSessionsAsync(stoppingToken);

        using var throttle = new SemaphoreSlim(_maxParallel);
        var inFlight = new ConcurrentDictionary<Guid, Task>();

        try
        {
            await foreach (var sessionId in _queue.ReadAllAsync(stoppingToken))
            {
                await throttle.WaitAsync(stoppingToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessSessionAsync(sessionId, stoppingToken);
                    }
                    finally
                    {
                        throttle.Release();
                        inFlight.TryRemove(sessionId, out _);
                    }
                }, stoppingToken);

                inFlight[sessionId] = task;
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }

        await Task.WhenAll(inFlight.Values);
        _logger.LogInformation("Exploration background service stopped.");
    }

    /// <summary>
    /// On startup, reconciles the DB with the (empty) in-memory queue: interrupted Running sessions
    /// are marked Failed, and never-started Pending sessions are re-enqueued so they run.
    /// </summary>
    private async Task RecoverInterruptedSessionsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            // Sessions left in Running were interrupted by the restart and cannot resume mid-browser-run.
            var orphaned = await db.ExplorationSessions
                .IgnoreQueryFilters()
                .Where(s => s.Status == ExplorationStatus.Running)
                .ToListAsync(stoppingToken);

            foreach (var session in orphaned)
            {
                session.Status = ExplorationStatus.Failed;
                session.ErrorMessage = "Interrupted by a service restart before completion. Re-run to try again.";
                session.CompletedAtUtc = DateTimeOffset.UtcNow;
            }

            if (orphaned.Count > 0)
            {
                await db.SaveChangesAsync(stoppingToken);
                _logger.LogWarning("Marked {Count} interrupted running session(s) as Failed after restart.", orphaned.Count);
            }

            // Pending sessions were queued but never processed — re-enqueue them to run now.
            var pending = await db.ExplorationSessions
                .IgnoreQueryFilters()
                .Where(s => s.Status == ExplorationStatus.Pending)
                .OrderBy(s => s.CreatedAtUtc)
                .Select(s => s.Id)
                .ToListAsync(stoppingToken);

            foreach (var id in pending)
            {
                await _queue.EnqueueAsync(id, stoppingToken);
            }

            if (pending.Count > 0)
            {
                _logger.LogInformation("Re-enqueued {Count} pending session(s) after restart.", pending.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to recover interrupted sessions on startup.");
        }
    }

    private async Task ProcessSessionAsync(Guid sessionId, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var agent = scope.ServiceProvider.GetRequiredService<IExplorerAgent>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        try
        {
            var session = await db.ExplorationSessions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == sessionId, stoppingToken);

            if (session is null)
            {
                _logger.LogWarning("Session {SessionId} not found; skipping.", sessionId);
                return;
            }

            if (session.Status == ExplorationStatus.Cancelled)
            {
                _logger.LogInformation("Session {SessionId} was cancelled before it could start.", sessionId);
                return;
            }

            _logger.LogInformation("Starting exploration session {SessionId}.", sessionId);
            await agent.RunAsync(session, stoppingToken);
            _logger.LogInformation("Exploration session {SessionId} finished with status {Status}.", sessionId, session.Status);

            await NotifyRunFinishedAsync(db, notifications, session, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error processing session {SessionId}.", sessionId);
        }
    }

    /// <summary>Creates a tenant notification summarizing how a run finished.</summary>
    private static async Task NotifyRunFinishedAsync(
        IApplicationDbContext db,
        INotificationService notifications,
        Domain.Entities.ExplorationSession session,
        CancellationToken cancellationToken)
    {
        string label;
        string category;
        if (session.SuiteId is { } suiteId)
        {
            category = "Suite";
            var name = await db.TestSuites.IgnoreQueryFilters()
                .Where(s => s.Id == suiteId).Select(s => s.Name).FirstOrDefaultAsync(cancellationToken);
            label = name is null ? "Test suite" : $"Suite \"{name}\"";
        }
        else if (session.ScenarioId is { } scenarioId)
        {
            category = "Run";
            var title = await db.Scenarios.IgnoreQueryFilters()
                .Where(s => s.Id == scenarioId).Select(s => s.Title).FirstOrDefaultAsync(cancellationToken);
            label = title is null ? "Scenario" : $"Scenario \"{title}\"";
        }
        else
        {
            category = "Run";
            label = "Application exploration";
        }

        var (level, verb) = session.Status switch
        {
            ExplorationStatus.Completed => ("success", "completed"),
            ExplorationStatus.Failed => ("error", "failed"),
            ExplorationStatus.Cancelled => ("warning", "was cancelled"),
            _ => ("info", "finished"),
        };

        var message = session.Status == ExplorationStatus.Failed && !string.IsNullOrWhiteSpace(session.ErrorMessage)
            ? $"{label} {verb}: {session.ErrorMessage}"
            : $"{label} {verb}.";

        await notifications.NotifyAsync(
            session.TenantId,
            title: $"{label} {verb}",
            message: message,
            level: level,
            category: category,
            entityType: nameof(Domain.Entities.ExplorationSession),
            entityId: session.Id,
            cancellationToken: cancellationToken);
    }
}
