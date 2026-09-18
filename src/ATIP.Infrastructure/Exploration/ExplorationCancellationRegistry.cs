using System.Collections.Concurrent;
using ATIP.Application.Common.Interfaces;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// In-memory implementation of <see cref="IExplorationCancellationRegistry"/>. Registered as a
/// singleton; only tracks sessions currently running on this instance (fine for the single-instance
/// deployment today — see repo memory for the horizontal-scaling caveat shared with the live-stream buffer).
/// </summary>
public sealed class ExplorationCancellationRegistry : IExplorationCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    public void Register(Guid sessionId, CancellationTokenSource cts) => _tokens[sessionId] = cts;

    public void Unregister(Guid sessionId) => _tokens.TryRemove(sessionId, out _);

    public bool RequestCancellation(Guid sessionId)
    {
        if (!_tokens.TryGetValue(sessionId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
