using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.ListExplorationSessions;

public sealed record ListExplorationSessionsQuery(Guid ProjectId) : IRequest<IReadOnlyList<ExplorationSessionDto>>;

public sealed class ListExplorationSessionsQueryHandler
    : IRequestHandler<ListExplorationSessionsQuery, IReadOnlyList<ExplorationSessionDto>>
{
    private readonly IApplicationDbContext _db;

    public ListExplorationSessionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExplorationSessionDto>> Handle(
        ListExplorationSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _db.ExplorationSessions
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return sessions.Select(ExplorationSessionDto.FromEntity).ToList();
    }
}
