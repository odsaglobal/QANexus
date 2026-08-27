using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Models;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Queries.ListProjects;

public sealed class ListProjectsQueryHandler : IRequestHandler<ListProjectsQuery, PagedResult<ProjectDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ListProjectsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ProjectDto>> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        // Tenant isolation is enforced by the global query filter on the DbContext.
        var query = _db.Projects
            .AsNoTracking()
            .Include(p => p.Environments)
            .Include(p => p.Members)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) || p.Key.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ProjectStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        query = ApplySort(query, request.SortBy, request.SortDescending);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userId = _currentUser.UserId;
        var isAdmin = _currentUser.SystemRole is SystemRole.TenantAdmin or SystemRole.PlatformAdmin;

        var dtos = items.Select(p =>
        {
            var role = isAdmin
                ? ProjectRole.Owner.ToString()
                : p.Members.FirstOrDefault(m => m.UserId == userId)?.Role.ToString();
            return ProjectDto.FromEntity(p, role);
        }).ToList();

        return PagedResult<ProjectDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }

    private static IQueryable<Project> ApplySort(IQueryable<Project> query, string? sortBy, bool descending)
    {
        // Allow-list to prevent arbitrary property injection into the ORDER BY clause.
        return (sortBy?.ToLowerInvariant()) switch
        {
            "name" => descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "status" => descending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "updated" => descending ? query.OrderByDescending(p => p.UpdatedAtUtc) : query.OrderBy(p => p.UpdatedAtUtc),
            _ => descending ? query.OrderByDescending(p => p.CreatedAtUtc) : query.OrderBy(p => p.CreatedAtUtc)
        };
    }
}
