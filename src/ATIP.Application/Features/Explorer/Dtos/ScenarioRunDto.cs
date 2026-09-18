using System.Text.Json;
using ATIP.Application.Features.Explorer.Agent;
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
    public string? ScreenshotPath { get; init; }

    /// <summary>Per-assertion verdicts, when this step's expectation was compiled. Empty otherwise.</summary>
    public IReadOnlyList<StepCheckResult> Checks { get; init; } = [];

    public static ScenarioStepResultDto FromEntity(ScenarioStepResult r) => new()
    {
        StepOrder = r.StepOrder,
        Action = r.Action,
        Status = r.Status.ToString(),
        Detail = r.Detail,
        Url = r.Url,
        ScreenshotPath = r.ScreenshotPath,
        Checks = StepChecksJson.Read(r.ChecksJson),
    };
}

/// <summary>
/// Reads the stored per-assertion breakdown. A run's report must render even if that column holds
/// something unexpected, so a parse failure degrades to "no breakdown" rather than failing the request.
/// </summary>
public static class StepChecksJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<StepCheckResult> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StepCheckResult>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// One recorded step result for a session, tagged with which scenario it belongs to — used to render a
/// full, ordered, per-scenario execution trace (steps + screenshots + validation detail) for an
/// Executions "View", whether the session ran a single scenario or an entire suite.
/// </summary>
public sealed record SessionStepResultDto
{
    public required Guid ScenarioId { get; init; }
    public required string ScenarioTitle { get; init; }
    public required int StepOrder { get; init; }
    public required string Action { get; init; }
    public required string Status { get; init; }
    public string? Detail { get; init; }
    public string? Url { get; init; }
    public string? ScreenshotPath { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Per-assertion verdicts, when this step's expectation was compiled. Empty otherwise.</summary>
    public IReadOnlyList<StepCheckResult> Checks { get; init; } = [];

    public static SessionStepResultDto FromEntity(ScenarioStepResult r, string scenarioTitle) => new()
    {
        ScenarioId = r.ScenarioId,
        ScenarioTitle = scenarioTitle,
        StepOrder = r.StepOrder,
        Action = r.Action,
        Status = r.Status.ToString(),
        Detail = r.Detail,
        Url = r.Url,
        ScreenshotPath = r.ScreenshotPath,
        CreatedAtUtc = r.CreatedAtUtc,
        Checks = StepChecksJson.Read(r.ChecksJson),
    };
}
