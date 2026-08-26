using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Users.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Users.Commands.SyncCurrentUserProfile;

/// <summary>
/// Updates the signed-in user's email/display name from the identity provider profile.
/// Auth0 access tokens often omit email/name, so the SPA (which has them from the ID token)
/// pushes them here to replace the synthetic placeholder created during JIT provisioning.
/// </summary>
public sealed record SyncCurrentUserProfileCommand(string? Email, string? Name)
    : IRequest<UserDto>;

public sealed class SyncCurrentUserProfileCommandHandler
    : IRequestHandler<SyncCurrentUserProfileCommand, UserDto>
{
    private const string SyntheticEmailSuffix = "@users.noreply";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SyncCurrentUserProfileCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(
        SyncCurrentUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("No user context.");

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        var changed = false;

        var name = request.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(name) && user.DisplayName != name)
        {
            user.DisplayName = name;
            changed = true;
        }

        // Only replace the email when the current one is a synthetic placeholder — never clobber a
        // real email, and never with an empty value.
        var email = request.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(email)
            && !email.EndsWith(SyntheticEmailSuffix, StringComparison.OrdinalIgnoreCase)
            && user.Email.EndsWith(SyntheticEmailSuffix, StringComparison.OrdinalIgnoreCase)
            && !await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != user.Id && u.Email == email, cancellationToken))
        {
            user.Email = email;
            changed = true;
        }

        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return UserDto.FromEntity(user);
    }
}
