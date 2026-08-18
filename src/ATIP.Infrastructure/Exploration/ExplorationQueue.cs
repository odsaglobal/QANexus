using System.Threading.Channels;
using ATIP.Application.Common.Interfaces;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// In-memory, unbounded channel that queues exploration session IDs for the background service.
/// Survives app restarts only if the DB still shows Pending sessions (re-queue logic can be added).
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
