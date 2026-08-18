using ATIP.Application.Features.Auth.Dtos;
using MediatR;

namespace ATIP.Application.Features.Auth.Commands.RegisterTenant;

/// <summary>
/// Bootstraps a new organization: creates the <c>Tenant</c> together with its first user,
/// who is granted <c>TenantAdmin</c>. Returns an access token so the caller is immediately signed in.
/// </summary>
public sealed record RegisterTenantCommand : IRequest<AuthResultDto>
{
    public required string OrganizationName { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string DisplayName { get; init; }
}
