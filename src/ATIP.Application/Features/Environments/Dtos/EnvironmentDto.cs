using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Environments.Dtos;

/// <summary>Read model for a project environment.</summary>
public sealed record EnvironmentDto
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string BaseUrl { get; init; }

    public bool IsDefault { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static EnvironmentDto FromEntity(Domain.Entities.Environment e) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        Name = e.Name,
        Type = e.Type.ToString(),
        BaseUrl = e.BaseUrl,
        IsDefault = e.IsDefault,
        CreatedAtUtc = e.CreatedAtUtc
    };
}
