using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Queries.GetProject;

public sealed class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, ProjectDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetProjectQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Environments)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.Id);

        var isAdmin = _currentUser.SystemRole is SystemRole.TenantAdmin or SystemRole.PlatformAdmin;
        var role = isAdmin
            ? ProjectRole.Owner.ToString()
            : project.Members.FirstOrDefault(m => m.UserId == _currentUser.UserId)?.Role.ToString();

        return ProjectDto.FromEntity(project, role);
    }
}

