using ATIP.Domain.Entities;

namespace ATIP.Application.Features.TestSuites.Dtos;

/// <summary>Read model for a test suite and its ordered scenarios.</summary>
public sealed record TestSuiteDto
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required int ScenarioCount { get; init; }
    public required IReadOnlyList<TestSuiteScenarioDto> Scenarios { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static TestSuiteDto FromEntity(TestSuite suite) => new()
    {
        Id = suite.Id,
        ProjectId = suite.ProjectId,
        Name = suite.Name,
        Description = suite.Description,
        ScenarioCount = suite.Scenarios.Count,
        Scenarios = suite.Scenarios
            .OrderBy(s => s.Order)
            .Select(TestSuiteScenarioDto.FromEntity)
            .ToList(),
        CreatedAtUtc = suite.CreatedAtUtc,
    };
}

/// <summary>A scenario's placement within a suite.</summary>
public sealed record TestSuiteScenarioDto
{
    public required Guid ScenarioId { get; init; }
    public required int Order { get; init; }
    public string? Title { get; init; }

    public static TestSuiteScenarioDto FromEntity(TestSuiteScenario s) => new()
    {
        ScenarioId = s.ScenarioId,
        Order = s.Order,
        Title = s.Scenario?.Title,
    };
}
