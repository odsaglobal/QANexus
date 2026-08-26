using System.Text.Json;
using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Requirements.Dtos;

/// <summary>Full requirement read model including the extracted module → feature → story graph.</summary>
public sealed record RequirementDetailDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string SourceType { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TextPreview { get; init; }
    public string? Content { get; init; }
    public required IReadOnlyList<ModuleDto> Modules { get; init; }

    public static RequirementDetailDto FromEntity(Requirement r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        SourceType = r.SourceType.ToString(),
        Status = r.Status.ToString(),
        ErrorMessage = r.ErrorMessage,
        TextPreview = r.ExtractedText is null
            ? null
            : r.ExtractedText[..Math.Min(2000, r.ExtractedText.Length)],
        Content = r.ExtractedText,
        Modules = r.Modules
            .OrderBy(m => m.Name)
            .Select(ModuleDto.FromEntity)
            .ToList(),
    };
}

public sealed record ModuleDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<FeatureDto> Features { get; init; }

    public static ModuleDto FromEntity(RequirementModule m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        Features = m.Features.OrderBy(f => f.Name).Select(FeatureDto.FromEntity).ToList(),
    };
}

public sealed record FeatureDto
{
    public required Guid Id { get; init; }
    public required Guid ModuleId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Priority { get; init; }
    public required IReadOnlyList<string> BusinessRules { get; init; }
    public int ScenarioCount { get; init; }
    public required IReadOnlyList<UserStoryDto> UserStories { get; init; }

    public static FeatureDto FromEntity(Feature f) => new()
    {
        Id = f.Id,
        ModuleId = f.ModuleId,
        Name = f.Name,
        Description = f.Description,
        Priority = f.Priority.ToString(),
        BusinessRules = Deserialize(f.BusinessRulesJson),
        ScenarioCount = f.Scenarios.Count,
        UserStories = f.UserStories.Select(UserStoryDto.FromEntity).ToList(),
    };

    internal static IReadOnlyList<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record UserStoryDto
{
    public required Guid Id { get; init; }
    public required string AsA { get; init; }
    public required string IWant { get; init; }
    public string? SoThat { get; init; }
    public required IReadOnlyList<string> AcceptanceCriteria { get; init; }

    public static UserStoryDto FromEntity(UserStory s) => new()
    {
        Id = s.Id,
        AsA = s.AsA,
        IWant = s.IWant,
        SoThat = s.SoThat,
        AcceptanceCriteria = FeatureDto.Deserialize(s.AcceptanceCriteriaJson),
    };
}
