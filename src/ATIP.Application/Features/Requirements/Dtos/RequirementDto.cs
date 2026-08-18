using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Requirements.Dtos;

/// <summary>Summary read model for an uploaded requirement document.</summary>
public sealed record RequirementDto
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string SourceType { get; init; }
    public required string Status { get; init; }
    public int ModuleCount { get; init; }
    public int FeatureCount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? AnalyzedAtUtc { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static RequirementDto FromEntity(Requirement r) => new()
    {
        Id = r.Id,
        ProjectId = r.ProjectId,
        Name = r.Name,
        SourceType = r.SourceType.ToString(),
        Status = r.Status.ToString(),
        ModuleCount = r.Modules.Count,
        FeatureCount = r.Modules.Sum(m => m.Features.Count),
        ErrorMessage = r.ErrorMessage,
        AnalyzedAtUtc = r.AnalyzedAtUtc,
        CreatedAtUtc = r.CreatedAtUtc,
    };
}
