using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Enums;
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

    /// <summary>
    /// When true, a RUN may spend one focused AI turn to re-locate a broken locator and repair the
    /// step's recording. When false, runs stay fully deterministic and never call the LLM.
    /// </summary>
    public bool AutoHealEnabled { get; init; } = true;

    /// <summary>Ordered list of steps — the full list replaces whatever was stored previously.</summary>
    public required IReadOnlyList<UpdateScenarioStepRequest> Steps { get; init; }
}

public sealed record UpdateScenarioStepRequest
{
    public required string Action { get; init; }

    public string? ExpectedResult { get; init; }

    /// <summary>
    /// Which system this step drives: Web, Api, Mobile or Database. Defaults to Web so existing
    /// clients keep working unchanged.
    /// </summary>
    public string Platform { get; init; } = nameof(TestPlatform.Web);

    /// <summary>
    /// Explicit engine verb for a step that is authored rather than recorded, e.g. <c>request</c>
    /// or <c>query</c>.
    /// </summary>
    /// <remarks>
    /// A browser step can be described in prose and discovered by the agent, because a page is
    /// there to be looked at. An API call or a SQL check cannot: there is no screen to explore, so
    /// the method, URL or statement has to be stated. Supplying a kind here is what turns a step
    /// into a deterministic recording without a discovery run ever happening.
    /// </remarks>
    public string? Kind { get; init; }

    /// <summary>Payload for the verb: the request body, the SQL text, the value to type.</summary>
    public string? Value { get; init; }

    /// <summary>Target for the verb: the URL, the element descriptor, the key.</summary>
    public string? Target { get; init; }

    /// <summary>Verb-specific arguments — <c>method</c>, <c>url</c>, <c>connection</c>, <c>expectedStatus</c>.</summary>
    public Dictionary<string, string?>? Options { get; init; }
}
