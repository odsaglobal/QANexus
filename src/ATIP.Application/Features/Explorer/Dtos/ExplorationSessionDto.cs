using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Explorer.Dtos;

public sealed record ExplorationSessionDto
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid EnvironmentId { get; init; }
    public Guid? FeatureId { get; init; }
    public required string Status { get; init; }
    public string? SeedUrl { get; init; }
    public required int MaxPages { get; init; }
    public required int MaxDepth { get; init; }
    public int PagesDiscovered { get; init; }
    public int ElementsDiscovered { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static ExplorationSessionDto FromEntity(ExplorationSession s) => new()
    {
        Id = s.Id,
        ProjectId = s.ProjectId,
        EnvironmentId = s.EnvironmentId,
        FeatureId = s.FeatureId,
        Status = s.Status.ToString(),
        SeedUrl = s.SeedUrl,
        MaxPages = s.MaxPages,
        MaxDepth = s.MaxDepth,
        PagesDiscovered = s.PagesDiscovered,
        ElementsDiscovered = s.ElementsDiscovered,
        StartedAtUtc = s.StartedAtUtc,
        CompletedAtUtc = s.CompletedAtUtc,
        ErrorMessage = s.ErrorMessage,
        CreatedAtUtc = s.CreatedAtUtc,
    };
}
