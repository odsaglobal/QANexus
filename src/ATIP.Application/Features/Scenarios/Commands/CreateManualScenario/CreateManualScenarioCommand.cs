using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.CreateManualScenario;

/// <summary>Creates one manually-authored scenario (Source = Manual) with ordered steps.</summary>
public sealed record CreateManualScenarioCommand : IRequest<ScenarioDto>
{
    public Guid ProjectId { get; init; }
    public Guid FeatureId { get; init; }
    public required string Title { get; init; }
    public string Type { get; init; } = "Positive";
    public string Priority { get; init; } = "Medium";
    public string Risk { get; init; } = "Medium";
    public string? Preconditions { get; init; }
    public string? ExpectedResult { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public required IReadOnlyList<CreateManualScenarioStep> Steps { get; init; }
}

public sealed record CreateManualScenarioStep
{
    public required string Action { get; init; }
    public string? ExpectedResult { get; init; }
}
