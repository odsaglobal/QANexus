using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Projects.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Queries.GetProject;

public sealed class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, ProjectDto>
{
    private readonly IApplicationDbContext _db;

    public GetProjectQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProjectDto> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Environments)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.Id);

        return ProjectDto.FromEntity(project);
    }
}
