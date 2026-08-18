using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Queries.ListScenarios;

/// <summary>
/// Lists scenarios for a project, optionally filtered to a single feature. Returns the full
/// scenario with steps so the explorer UI can render everything without extra round-trips.
/// </summary>
public sealed record ListScenariosQuery : IRequest<IReadOnlyList<ScenarioDto>>
{
    public Guid ProjectId { get; init; }
    public Guid? FeatureId { get; init; }
}
