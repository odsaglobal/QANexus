using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.SyncTestRailScenarios;

public sealed record SyncTestRailScenariosCommand : IRequest<IReadOnlyList<ScenarioDto>>
{
    public Guid ProjectId { get; init; }
    public Guid FeatureId { get; init; }

    /// <summary>TestRail project id (numeric) in the external TestRail workspace.</summary>
    public int TestRailProjectId { get; init; }

    /// <summary>Optional TestRail suite id filter.</summary>
    public int? SuiteId { get; init; }

    /// <summary>Optional TestRail section id filter.</summary>
    public int? SectionId { get; init; }
}
