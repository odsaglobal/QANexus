using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Explorer.Dtos;

/// <summary>Latest scenario-scoped run: overall status plus per-step results.</summary>
public sealed record ScenarioRunDto
{
    public required Guid SessionId { get; init; }
    public required Guid ScenarioId { get; init; }
    public required string Status { get; init; }
    public required string Outcome { get; init; }
    public int PassedSteps { get; init; }
    public int HealedSteps { get; init; }
    public int FailedSteps { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required IReadOnlyList<ScenarioStepResultDto> Steps { get; init; }
}

public sealed record ScenarioStepResultDto
{
    public required int StepOrder { get; init; }
    public required string Action { get; init; }
    public required string Status { get; init; }
    public string? Detail { get; init; }
    public string? Url { get; init; }

    public static ScenarioStepResultDto FromEntity(ScenarioStepResult r) => new()
    {
        StepOrder = r.StepOrder,
        Action = r.Action,
        Status = r.Status.ToString(),
        Detail = r.Detail,
        Url = r.Url,
    };
}
