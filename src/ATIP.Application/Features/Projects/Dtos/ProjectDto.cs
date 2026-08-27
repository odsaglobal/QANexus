using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Projects.Dtos;

/// <summary>Read model for a project returned by queries.</summary>
public sealed record ProjectDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Key { get; init; }

    public string? Description { get; init; }

    public required string Status { get; init; }

    public int EnvironmentCount { get; init; }

    public int MemberCount { get; init; }

    /// <summary>The requesting user's project role (Owner/Editor/Viewer), when resolvable.</summary>
    public string? CurrentUserRole { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public static ProjectDto FromEntity(Project p, string? currentUserRole = null) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Key = p.Key,
        Description = p.Description,
        Status = p.Status.ToString(),
        EnvironmentCount = p.Environments.Count,
        MemberCount = p.Members.Count,
        CurrentUserRole = currentUserRole,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc
    };
}
