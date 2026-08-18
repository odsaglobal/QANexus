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

    private async Task ProcessSessionAsync(Guid sessionId, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var agent = scope.ServiceProvider.GetRequiredService<IExplorerAgent>();

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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error processing session {SessionId}.", sessionId);
        }
    }
}
