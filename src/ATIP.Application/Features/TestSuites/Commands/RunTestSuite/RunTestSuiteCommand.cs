using ATIP.Application.Common.Security;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.TestSuites.Commands.RunTestSuite;

/// <summary>
/// Runs an entire suite as a single exploration session that walks every scenario's steps
/// sequentially in one shared browser session against the chosen environment.
/// </summary>
public sealed record RunTestSuiteCommand : IRequest<ExplorationSessionDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid SuiteId { get; init; }

    /// <summary>Optional environment; when omitted the project's default (or first) environment is used.</summary>
    public Guid? EnvironmentId { get; init; }

    /// <summary>
    /// Optional filters narrowing which of the suite's scenarios run. Each facet matches ANY of its
    /// values, and the facets are combined with AND — so "Negative" + "Critical" runs the critical
    /// negative cases. Empty facets are ignored.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    public IReadOnlyList<string>? Types { get; init; }

    public IReadOnlyList<string>? Priorities { get; init; }

    /// <summary>
    /// Optional explicit list of the suite's scenarios to run, for when the user unchecks individual
    /// test cases. Combined with the facets using AND. Omitted or empty means "every match".
    /// </summary>
    public IReadOnlyList<Guid>? ScenarioIds { get; init; }

    /// <summary>
    /// True (the default) replays each scenario's already-recorded steps deterministically — the same
    /// engine a single scenario's "Run" uses, with zero MCP subprocess and zero LLM tokens. False runs
    /// the AI/MCP goal-driven exploration instead, the same as a single scenario's "Explore".
    /// </summary>
    public bool ExecuteSavedSteps { get; init; } = true;

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
