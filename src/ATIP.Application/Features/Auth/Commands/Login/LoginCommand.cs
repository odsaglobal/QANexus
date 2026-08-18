using ATIP.Application.Features.Auth.Dtos;
using MediatR;

namespace ATIP.Application.Features.Auth.Commands.Login;

/// <summary>Authenticates a local account and returns an access token on success.</summary>
public sealed record LoginCommand : IRequest<AuthResultDto>
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}
