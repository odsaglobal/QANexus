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

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
