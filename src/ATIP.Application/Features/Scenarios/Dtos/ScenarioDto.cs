using System.Text.Json;
using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Scenarios.Dtos;

/// <summary>Read model for a generated test scenario, including its ordered steps.</summary>
public sealed record ScenarioDto
{
    public required Guid Id { get; init; }
    public required Guid FeatureId { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public required string Risk { get; init; }
    public required string Source { get; init; }
    public string? Preconditions { get; init; }
    public string? ExpectedResult { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<ScenarioStepDto> Steps { get; init; }
    /// <summary>AI-discovered steps from a goal-driven exploration, awaiting the user's confirmation.</summary>
    public IReadOnlyList<ProposedStep> ProposedSteps { get; init; } = [];
    /// <summary>True when a previous step set was backed up and can be restored.</summary>
    public bool CanRevert { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static ScenarioDto FromEntity(Scenario s) => new()
    {
        Id = s.Id,
        FeatureId = s.FeatureId,
        Title = s.Title,
        Type = s.Type.ToString(),
        Priority = s.Priority.ToString(),
        Risk = s.Risk.ToString(),
        Source = s.Source.ToString(),
        Preconditions = s.Preconditions,
        ExpectedResult = s.ExpectedResult,
        Tags = Deserialize(s.TagsJson),
        Steps = s.Steps
            .OrderBy(st => st.Order)
            .Select(ScenarioStepDto.FromEntity)
            .ToList(),
        ProposedSteps = DeserializeProposed(s.ProposedStepsJson),
        CanRevert = !string.IsNullOrWhiteSpace(s.PreviousStepsJson),
        CreatedAtUtc = s.CreatedAtUtc,
    };

    private static IReadOnlyList<ProposedStep> DeserializeProposed(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProposedStep>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> Deserialize(string? json)
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

public sealed record ScenarioStepDto
{
    public required int Order { get; init; }
    public required string Action { get; init; }
    public string? ExpectedResult { get; init; }
    public bool NeedsReview { get; init; }
    public string? ReviewReason { get; init; }
    public bool HasRecording { get; init; }

    public static ScenarioStepDto FromEntity(ScenarioStep s) => new()
    {
        Order = s.Order,
        Action = s.Action,
        ExpectedResult = s.ExpectedResult,
        NeedsReview = s.NeedsReview,
        ReviewReason = s.ReviewReason,
        HasRecording = !string.IsNullOrWhiteSpace(s.RecordedActionsJson),
    };
}
