using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Users.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Users.Commands.ChangeUserSystemRole;

/// <summary>
/// Changes a workspace member's system role (Member ↔ TenantAdmin). TenantAdmin only.
/// Guards against removing the last admin.
/// </summary>
public sealed record ChangeUserSystemRoleCommand(Guid UserId, string Role) : IRequest<UserDto>;

public sealed class ChangeUserSystemRoleCommandHandler
    : IRequestHandler<ChangeUserSystemRoleCommand, UserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ChangeUserSystemRoleCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(ChangeUserSystemRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        if (_currentUser.SystemRole is not (SystemRole.TenantAdmin or SystemRole.PlatformAdmin))
        {
            throw new ForbiddenAccessException("Only a workspace admin can change member roles.");
        }

        if (!Enum.TryParse<SystemRole>(request.Role, ignoreCase: true, out var role)
            || role is SystemRole.PlatformAdmin)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Role), "Role must be 'Member' or 'TenantAdmin'."),
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        // Demoting a TenantAdmin: never leave the workspace without one.
        if (user.SystemRole == SystemRole.TenantAdmin && role != SystemRole.TenantAdmin)
        {
            var adminCount = await _db.Users
                .CountAsync(u => u.SystemRole == SystemRole.TenantAdmin, cancellationToken);
            if (adminCount <= 1)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Role), "The workspace must have at least one admin."),
                });
            }
        }

        if (user.SystemRole != role)
        {
            user.SystemRole = role;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return UserDto.FromEntity(user);
    }
}
