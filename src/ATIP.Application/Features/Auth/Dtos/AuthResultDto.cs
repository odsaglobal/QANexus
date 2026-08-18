namespace ATIP.Application.Features.Auth.Dtos;

/// <summary>Returned to clients after a successful register or login.</summary>
public sealed record AuthResultDto
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required Guid UserId { get; init; }

    public required Guid TenantId { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required string SystemRole { get; init; }
}
