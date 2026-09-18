using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Users.Dtos;

/// <summary>A workspace member (tenant user) read model.</summary>
public sealed record UserDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsFederated { get; init; }
    public DateTimeOffset? LastLoginAtUtc { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The tenant (client workspace) the user belongs to. Name/Slug are populated only when
    /// the Tenant navigation is loaded (e.g. the current-user endpoint); null in the members list.</summary>
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public bool TenantIsOnboarded { get; init; }

    public static UserDto FromEntity(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        DisplayName = u.DisplayName,
        Role = u.SystemRole.ToString(),
        IsActive = u.IsActive,
        IsFederated = u.IsFederated,
        LastLoginAtUtc = u.LastLoginAtUtc,
        CreatedAtUtc = u.CreatedAtUtc,
        TenantId = u.TenantId,
        TenantName = u.Tenant?.Name,
        TenantSlug = u.Tenant?.Slug,
        TenantIsOnboarded = u.Tenant?.IsOnboarded ?? true,
    };
}
