using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Commands.UpdateProject;

public sealed class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, ProjectDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateProjectCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProjectDto> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .Include(p => p.Environments)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.Id);

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.Status = Enum.Parse<ProjectStatus>(request.Status, ignoreCase: true);

        await _db.SaveChangesAsync(cancellationToken);

        return ProjectDto.FromEntity(project);
    }
}
