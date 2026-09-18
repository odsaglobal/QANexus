using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Commands.RemoveProjectMember;

/// <summary>Removes a member from a project. Owner only. Keeps at least one Owner.</summary>
public sealed record RemoveProjectMemberCommand : IRequest<Unit>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Owner;
}

public sealed class RemoveProjectMemberCommandHandler
    : IRequestHandler<RemoveProjectMemberCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public RemoveProjectMemberCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Unit> Handle(RemoveProjectMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await _db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == request.ProjectId && m.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProjectMember), request.UserId);

        if (member.Role == ProjectRole.Owner)
        {
            var ownerCount = await _db.ProjectMembers
                .CountAsync(m => m.ProjectId == request.ProjectId && m.Role == ProjectRole.Owner, cancellationToken);
            if (ownerCount <= 1)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.UserId), "A project must have at least one owner."),
                });
            }
        }

        _db.ProjectMembers.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
