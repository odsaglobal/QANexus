using ATIP.Application.Features.Explorer.Dtos;
using MediatR;

namespace ATIP.Application.Features.Explorer.Queries.GetLatestScenarioRun;

/// <summary>Returns the most recent scenario-scoped run (and step results) for a scenario, or null if never run.</summary>
public sealed record GetLatestScenarioRunQuery(Guid ProjectId, Guid ScenarioId) : IRequest<ScenarioRunDto?>;
