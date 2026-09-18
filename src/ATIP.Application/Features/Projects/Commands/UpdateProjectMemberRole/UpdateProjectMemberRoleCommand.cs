using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Commands.UpdateProjectMemberRole;

/// <summary>Changes an existing project member's role. Owner only. Keeps at least one Owner.</summary>
public sealed record UpdateProjectMemberRoleCommand : IRequest<ProjectMemberDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
    public required string Role { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Owner;
}

public sealed class UpdateProjectMemberRoleCommandHandler
    : IRequestHandler<UpdateProjectMemberRoleCommand, ProjectMemberDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateProjectMemberRoleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProjectMemberDto> Handle(UpdateProjectMemberRoleCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ProjectRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Role), "Role must be Viewer, Editor or Owner."),
            });
        }

        var member = await _db.ProjectMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ProjectId == request.ProjectId && m.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProjectMember), request.UserId);

        // Demoting the last Owner would orphan the project.
        if (member.Role == ProjectRole.Owner && role != ProjectRole.Owner)
        {
            var ownerCount = await _db.ProjectMembers
                .CountAsync(m => m.ProjectId == request.ProjectId && m.Role == ProjectRole.Owner, cancellationToken);
            if (ownerCount <= 1)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Role), "A project must have at least one owner."),
                });
            }
        }

        if (member.Role != role)
        {
            member.Role = role;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ProjectMemberDto.FromEntity(member);
    }
}
