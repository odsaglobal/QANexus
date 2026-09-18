using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Commands.AddProjectMember;

/// <summary>Adds a workspace user to a project with a role (or updates their role if already a member). Owner only.</summary>
public sealed record AddProjectMemberCommand : IRequest<ProjectMemberDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
    public required string Role { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Owner;
}

public sealed class AddProjectMemberCommandHandler
    : IRequestHandler<AddProjectMemberCommand, ProjectMemberDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AddProjectMemberCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProjectMemberDto> Handle(AddProjectMemberCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        if (!Enum.TryParse<ProjectRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Role), "Role must be Viewer, Editor or Owner."),
            });
        }

        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        var member = await _db.ProjectMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ProjectId == request.ProjectId && m.UserId == request.UserId, cancellationToken);

        if (member is null)
        {
            member = new ProjectMember
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                UserId = request.UserId,
                Role = role,
            };
            _db.ProjectMembers.Add(member);
        }
        else
        {
            member.Role = role;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Ensure the User navigation is populated for the response.
        member.User ??= user;
        return ProjectMemberDto.FromEntity(member);
    }
}
