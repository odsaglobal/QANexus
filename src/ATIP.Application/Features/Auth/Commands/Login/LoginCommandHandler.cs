using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Auth.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IDateTimeProvider _clock;

    public LoginCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IDateTimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Cross-tenant lookup: at login time we do not yet know the tenant context.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Uniform failure to avoid leaking which accounts exist (user enumeration protection).
        if (user is null || user.IsDeleted || !user.IsActive || user.PasswordHash is null ||
            !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        user.LastLoginAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwt.CreateAccessToken(user);

        return new AuthResultDto
        {
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            SystemRole = user.SystemRole.ToString()
        };
    }
}
