using ATIP.Domain.Entities;

namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Drives a single exploration session end-to-end: launches the browser, crawls the application,
/// discovers pages and elements, and persists everything. Implemented in Infrastructure.
/// </summary>
public interface IExplorerAgent
{
    Task RunAsync(ExplorationSession session, CancellationToken cancellationToken = default);
}
