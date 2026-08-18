using ATIP.Application.Features.TestSuites.Dtos;
using MediatR;

namespace ATIP.Application.Features.TestSuites.Commands.UpdateTestSuite;

/// <summary>Updates a suite's name/description and replaces its ordered scenario membership.</summary>
public sealed record UpdateTestSuiteCommand : IRequest<TestSuiteDto>
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Guid> ScenarioIds { get; init; } = [];
}
