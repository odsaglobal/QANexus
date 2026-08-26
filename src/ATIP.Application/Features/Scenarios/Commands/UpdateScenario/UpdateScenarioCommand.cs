using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.UpdateScenario;

/// <summary>
/// Updates a scenario's metadata and replaces its entire ordered step list.
/// Sending a full replacement (rather than individual step operations) keeps the API simple
/// and idempotent — the frontend is the authoritative source of step order.
/// </summary>
public sealed record UpdateScenarioCommand : IRequest<ScenarioDto>
{
    public Guid Id { get; init; }

    public required string Title { get; init; }

    public required string Type { get; init; }

    public required string Priority { get; init; }

    public required string Risk { get; init; }

    public string? Preconditions { get; init; }

    public string? ExpectedResult { get; init; }

    /// <summary>Optional Jira issue key this scenario traces to (e.g. PROJ-123).</summary>
    public string? JiraKey { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Ordered list of steps — the full list replaces whatever was stored previously.</summary>
    public required IReadOnlyList<UpdateScenarioStepRequest> Steps { get; init; }
}

public sealed record UpdateScenarioStepRequest
{
    public required string Action { get; init; }

    public string? ExpectedResult { get; init; }
}
