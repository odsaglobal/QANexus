using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Queries.ListProjectMembers;

/// <summary>Lists the members of a project. Any project member (Viewer+) may read.</summary>
public sealed record ListProjectMembersQuery(Guid ProjectId)
    : IRequest<IReadOnlyList<ProjectMemberDto>>, IProjectScopedRequest
{
    public ProjectRole RequiredRole => ProjectRole.Viewer;
}

public sealed class ListProjectMembersQueryHandler
    : IRequestHandler<ListProjectMembersQuery, IReadOnlyList<ProjectMemberDto>>
{
    private readonly IApplicationDbContext _db;

    public ListProjectMembersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProjectMemberDto>> Handle(
        ListProjectMembersQuery request,
        CancellationToken cancellationToken)
    {
        var members = await _db.ProjectMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.ProjectId == request.ProjectId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.User.DisplayName)
            .ToListAsync(cancellationToken);

        return members.Select(ProjectMemberDto.FromEntity).ToList();
    }
}
