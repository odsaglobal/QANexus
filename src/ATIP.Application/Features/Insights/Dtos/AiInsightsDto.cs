namespace ATIP.Application.Features.Insights.Dtos;

/// <summary>
/// A single actionable finding derived from execution history and authoring state.
/// </summary>
public sealed record AiInsightDto
{
    /// <summary>
    /// Deterministic identifier (e.g. <c>flaky:{scenarioId}:{stepOrder}</c>) so the same finding keeps
    /// the same id across refreshes. Lets the UI keep dismissals/expansion stable without persistence.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Reliability | Coverage | Quality | Maintenance.</summary>
    public required string Category { get; init; }

    /// <summary>Critical | Warning | Info.</summary>
    public required string Severity { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string SuggestedAction { get; init; }

    /// <summary>
    /// The concrete observations this finding was derived from. Every insight must be able to show
    /// its work — a recommendation with no evidence is indistinguishable from a guess.
    /// </summary>
    public required IReadOnlyList<string> Evidence { get; init; }

    public Guid? ProjectId { get; init; }

    public string? ProjectName { get; init; }

    public Guid? ScenarioId { get; init; }

    public string? ScenarioTitle { get; init; }

    /// <summary>When the underlying signal was last observed, where the signal is time-based.</summary>
    public DateTimeOffset? LastSeenUtc { get; init; }
}

/// <summary>Insight feed plus the coverage of the analysis that produced it.</summary>
public sealed record AiInsightsDto
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required int RunsAnalyzed { get; init; }

    public required int ScenariosAnalyzed { get; init; }

    public required int StepResultsAnalyzed { get; init; }

    public required IReadOnlyList<AiInsightDto> Insights { get; init; }
}
