using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Auth.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Auth.Commands.RegisterTenant;

public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, AuthResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;

    public RegisterTenantCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
    }

    public async Task<AuthResultDto> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailTaken)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    nameof(RegisterTenantCommand.Email),
                    "An account with this email already exists.")
            ]);
        }

        var slug = await GenerateUniqueSlugAsync(request.OrganizationName, cancellationToken);

        var tenant = new Tenant
        {
            Name = request.OrganizationName.Trim(),
            Slug = slug
        };

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            SystemRole = SystemRole.TenantAdmin
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwt.CreateAccessToken(user);

        return new AuthResultDto
        {
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.Id,
            TenantId = tenant.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            SystemRole = user.SystemRole.ToString()
        };
    }

    private async Task<string> GenerateUniqueSlugAsync(string organizationName, CancellationToken cancellationToken)
    {
        var baseSlug = SlugGenerator.Create(organizationName);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "org";
        }

        var candidate = baseSlug;
        var suffix = 1;
        while (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }
}
