namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Receives exploration session IDs for processing by the background service.
/// Implemented in Infrastructure with an in-memory Channel so no external broker is needed.
/// </summary>
public interface IExplorationQueue
{
    ValueTask EnqueueAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
