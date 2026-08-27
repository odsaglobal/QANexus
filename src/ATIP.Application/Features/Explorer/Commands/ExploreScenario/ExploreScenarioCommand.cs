using ATIP.Application.Common.Security;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Explorer.Commands.ExploreScenario;

/// <summary>Runs a scenario-scoped exploration: walks just this scenario's steps and records per-step results.</summary>
public sealed record ExploreScenarioCommand : IRequest<ExplorationSessionDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid ScenarioId { get; init; }

    /// <summary>When true, execute the scenario's saved steps as a test (Run) instead of goal-driven discovery (Explore).</summary>
    public bool ExecuteSavedSteps { get; init; }

    /// <summary>Optional environment; when omitted the project's default (or first) environment is used.</summary>
    public Guid? EnvironmentId { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
