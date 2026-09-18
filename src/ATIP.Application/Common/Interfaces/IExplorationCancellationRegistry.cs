namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Lets a cancellation request actually stop an in-flight exploration session's agent loop instead of
/// only flipping a database flag. The background service registers each running session's linked
/// <see cref="CancellationTokenSource"/> here when it starts, and unregisters it when the run ends.
/// Implemented in Infrastructure as a singleton in-memory registry.
/// </summary>
public interface IExplorationCancellationRegistry
{
    void Register(Guid sessionId, CancellationTokenSource cts);

    void Unregister(Guid sessionId);

    /// <summary>Requests cancellation of a running session. Returns true if a running session was found and signalled.</summary>
    bool RequestCancellation(Guid sessionId);
}
