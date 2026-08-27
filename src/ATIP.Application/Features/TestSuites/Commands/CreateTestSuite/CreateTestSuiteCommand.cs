using ATIP.Application.Common.Security;
using ATIP.Application.Features.TestSuites.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.TestSuites.Commands.CreateTestSuite;

/// <summary>Creates a suite with an ordered set of scenarios (by the order of <see cref="ScenarioIds"/>).</summary>
public sealed record CreateTestSuiteCommand : IRequest<TestSuiteDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Guid> ScenarioIds { get; init; } = [];

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
