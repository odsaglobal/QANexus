using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Tenants.Dtos;

/// <summary>Read model for a tenant (client workspace).</summary>
public sealed record TenantDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required bool IsOnboarded { get; init; }

    public static TenantDto FromEntity(Tenant t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
        IsOnboarded = t.IsOnboarded,
    };
}
