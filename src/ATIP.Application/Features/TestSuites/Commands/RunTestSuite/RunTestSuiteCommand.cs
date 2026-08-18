using ATIP.Application.Features.Explorer.Dtos;
using MediatR;

namespace ATIP.Application.Features.TestSuites.Commands.RunTestSuite;

/// <summary>
/// Runs an entire suite as a single exploration session that walks every scenario's steps
/// sequentially in one shared browser session against the chosen environment.
/// </summary>
public sealed record RunTestSuiteCommand : IRequest<ExplorationSessionDto>
{
    public Guid ProjectId { get; init; }
    public Guid SuiteId { get; init; }

    /// <summary>Optional environment; when omitted the project's default (or first) environment is used.</summary>
    public Guid? EnvironmentId { get; init; }
}
