using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.ListDiscoveredPages;

public sealed record ListDiscoveredPagesQuery(Guid SessionId) : IRequest<IReadOnlyList<DiscoveredPageDto>>;

public sealed class ListDiscoveredPagesQueryHandler
    : IRequestHandler<ListDiscoveredPagesQuery, IReadOnlyList<DiscoveredPageDto>>
{
    private readonly IApplicationDbContext _db;

    public ListDiscoveredPagesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DiscoveredPageDto>> Handle(
        ListDiscoveredPagesQuery request, CancellationToken cancellationToken)
    {
        var pages = await _db.DiscoveredPages
            .AsNoTracking()
            .Where(p => p.SessionId == request.SessionId)
            .OrderBy(p => p.DepthFromRoot)
            .ThenBy(p => p.Url)
            .ToListAsync(cancellationToken);

        return pages.Select(DiscoveredPageDto.FromEntity).ToList();
    }
}
