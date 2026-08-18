namespace ATIP.Application.Features.Dashboard.Dtos;

/// <summary>Tenant-scoped rollup used by the dashboard overview. All counts are real, from persisted data.</summary>
public sealed record DashboardSummaryDto
{
    public required int ProjectCount { get; init; }
    public required int RequirementCount { get; init; }
    public required int AnalyzedRequirementCount { get; init; }
    public required int FeatureCount { get; init; }
    public required int ScenarioCount { get; init; }
    public required int AiScenarioCount { get; init; }
    public required int ManualScenarioCount { get; init; }
    public required int TestRailScenarioCount { get; init; }
    public required int ExplorationSessionCount { get; init; }
    public required int DiscoveredPageCount { get; init; }
}
