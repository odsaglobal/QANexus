using ATIP.Domain.Entities;

namespace ATIP.Application.Features.ApiKeys.Dtos;

/// <summary>An API key read model (never includes the secret).</summary>
public sealed record ApiKeyDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Prefix { get; init; }
    public DateTimeOffset? LastUsedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static ApiKeyDto FromEntity(ApiKey k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        Prefix = k.Prefix,
        LastUsedAtUtc = k.LastUsedAtUtc,
        ExpiresAtUtc = k.ExpiresAtUtc,
        RevokedAtUtc = k.RevokedAtUtc,
        IsActive = k.IsActive,
        CreatedAtUtc = k.CreatedAtUtc,
    };
}

/// <summary>Returned once on creation and includes the plaintext key — shown to the user a single time.</summary>
public sealed record CreatedApiKeyDto
{
    public required ApiKeyDto Key { get; init; }
    public required string PlainText { get; init; }
}
