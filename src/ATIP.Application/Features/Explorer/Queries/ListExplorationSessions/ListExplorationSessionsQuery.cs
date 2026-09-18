using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.ListExplorationSessions;

/// <param name="ActiveOnly">
/// Restricts the result to sessions that are still in flight. The UI polls this every few seconds to
/// flag which scenarios are running, so it must stay O(in-flight sessions) rather than growing with
/// the project's entire exploration history.
/// </param>
public sealed record ListExplorationSessionsQuery(Guid ProjectId, bool ActiveOnly = false)
    : IRequest<IReadOnlyList<ExplorationSessionDto>>;

public sealed class ListExplorationSessionsQueryHandler
    : IRequestHandler<ListExplorationSessionsQuery, IReadOnlyList<ExplorationSessionDto>>
{
    private readonly IApplicationDbContext _db;

    public ListExplorationSessionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExplorationSessionDto>> Handle(
        ListExplorationSessionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ExplorationSessions
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId);

        if (request.ActiveOnly)
        {
            query = query.Where(s =>
                s.Status == ExplorationStatus.Pending || s.Status == ExplorationStatus.Running);
        }

        var sessions = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return sessions.Select(ExplorationSessionDto.FromEntity).ToList();
    }
}
