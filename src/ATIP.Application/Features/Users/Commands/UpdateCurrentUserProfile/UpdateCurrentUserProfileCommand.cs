using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Users.Dtos;
using ATIP.Domain.Entities;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Users.Commands.UpdateCurrentUserProfile;

/// <summary>
/// Explicitly updates the signed-in user's own display name (from the Profile page). Unlike the
/// identity-provider sync, this always applies the provided value.
/// </summary>
public sealed record UpdateCurrentUserProfileCommand(string DisplayName) : IRequest<UserDto>;

public sealed class UpdateCurrentUserProfileCommandHandler
    : IRequestHandler<UpdateCurrentUserProfileCommand, UserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateCurrentUserProfileCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(
        UpdateCurrentUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("No user context.");

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.DisplayName), "Display name is required."),
            });
        }

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (user.DisplayName != displayName)
        {
            user.DisplayName = displayName;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return UserDto.FromEntity(user);
    }
}
