namespace ATIP.Application.Features.Reports.Dtos;

/// <summary>Tenant-wide quality metrics computed from real runs and scenario step results.</summary>
public sealed record ReportsSummaryDto
{
    public required int TotalRuns { get; init; }
    public required int CompletedRuns { get; init; }
    public required int FailedRuns { get; init; }
    public required int RunningRuns { get; init; }

    public required int StepsPassed { get; init; }
    public required int StepsHealed { get; init; }
    public required int StepsFailed { get; init; }
    public required int StepPassRate { get; init; }

    public required IReadOnlyList<ScenarioTypeSlice> ScenarioMix { get; init; }
    public required IReadOnlyList<WeeklyTrendPoint> WeeklyTrend { get; init; }
    public required IReadOnlyList<FailingScenario> TopFailingScenarios { get; init; }
    public required IReadOnlyList<RecentRun> RecentRuns { get; init; }
}

public sealed record ScenarioTypeSlice(string Type, int Count);

public sealed record WeeklyTrendPoint(string Week, int Passed, int Failed);

public sealed record FailingScenario(string Title, int FailedSteps);

public sealed record RecentRun(
    Guid SessionId,
    string Label,
    string ProjectName,
    string Status,
    DateTimeOffset? StartedAtUtc,
    int? DurationSeconds);
