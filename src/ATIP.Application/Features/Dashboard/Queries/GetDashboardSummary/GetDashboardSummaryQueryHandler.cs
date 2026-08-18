using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Dashboard.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _db;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        // Tenant scoping is applied automatically by the global query filters.
        var projectCount = await _db.Projects.AsNoTracking().CountAsync(cancellationToken);
        var requirementCount = await _db.Requirements.AsNoTracking().CountAsync(cancellationToken);
        var analyzedRequirementCount = await _db.Requirements.AsNoTracking()
            .CountAsync(r => r.Status == RequirementStatus.Analyzed, cancellationToken);
        var featureCount = await _db.Features.AsNoTracking().CountAsync(cancellationToken);

        var scenarioCount = await _db.Scenarios.AsNoTracking().CountAsync(cancellationToken);
        var aiScenarioCount = await _db.Scenarios.AsNoTracking()
            .CountAsync(s => s.Source == ScenarioSource.AiGenerated, cancellationToken);
        var manualScenarioCount = await _db.Scenarios.AsNoTracking()
            .CountAsync(s => s.Source == ScenarioSource.Manual, cancellationToken);
        var testRailScenarioCount = await _db.Scenarios.AsNoTracking()
            .CountAsync(s => s.Source == ScenarioSource.TestRail, cancellationToken);

        var explorationSessionCount = await _db.ExplorationSessions.AsNoTracking().CountAsync(cancellationToken);
        var discoveredPageCount = await _db.DiscoveredPages.AsNoTracking().CountAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            ProjectCount = projectCount,
            RequirementCount = requirementCount,
            AnalyzedRequirementCount = analyzedRequirementCount,
            FeatureCount = featureCount,
            ScenarioCount = scenarioCount,
            AiScenarioCount = aiScenarioCount,
            ManualScenarioCount = manualScenarioCount,
            TestRailScenarioCount = testRailScenarioCount,
            ExplorationSessionCount = explorationSessionCount,
            DiscoveredPageCount = discoveredPageCount,
        };
    }
}
