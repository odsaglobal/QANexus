using System.Threading.Channels;
using ATIP.Application.Common.Interfaces;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// In-memory, unbounded channel that queues exploration session IDs for the background service.
/// The DB is the durable source of truth: sessions are persisted (Pending) before being enqueued,
/// and <c>ExplorationBackgroundService</c> re-enqueues Pending sessions (and fails orphaned Running
/// ones) on startup, so runs survive an app restart.
/// </summary>
public sealed class ExplorationQueue : IExplorationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(sessionId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
