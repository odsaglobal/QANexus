using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Models;
using ATIP.Application.Features.Explorer.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.ListDiscoveredElements;

public sealed record ListDiscoveredElementsQuery : PaginationQuery, IRequest<PagedResult<DiscoveredElementDto>>
{
    public Guid ProjectId { get; init; }
    public Guid? PageId { get; init; }
    public string? Role { get; init; }
}

public sealed class ListDiscoveredElementsQueryHandler
    : IRequestHandler<ListDiscoveredElementsQuery, PagedResult<DiscoveredElementDto>>
{
    private readonly IApplicationDbContext _db;

    public ListDiscoveredElementsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<DiscoveredElementDto>> Handle(
        ListDiscoveredElementsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.DiscoveredElements
            .AsNoTracking()
            .Include(e => e.Locators)
            .Where(e => e.ProjectId == request.ProjectId);

        if (request.PageId.HasValue)
        {
            query = query.Where(e => e.PageId == request.PageId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(e => e.Role != null && e.Role.ToLower() == request.Role.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(e =>
                (e.Name != null && e.Name.ToLower().Contains(term)) ||
                (e.AriaLabel != null && e.AriaLabel.ToLower().Contains(term)) ||
                (e.TextContent != null && e.TextContent.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.ConfidenceScore)
            .ThenBy(e => e.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<DiscoveredElementDto>.Create(
            items.Select(DiscoveredElementDto.FromEntity).ToList(),
            request.Page, request.PageSize, total);
    }
}
