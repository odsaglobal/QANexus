using System.Text.Json.Serialization;

namespace ATIP.Application.Features.Scenarios.Generation;

/// <summary>JSON shape the LLM returns when generating scenarios for a feature.</summary>
public sealed class ScenarioGenerationResult
{
    [JsonPropertyName("scenarios")]
    public List<GeneratedScenario> Scenarios { get; set; } = [];
}

public sealed class GeneratedScenario
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("risk")]
    public string? Risk { get; set; }

    [JsonPropertyName("preconditions")]
    public string? Preconditions { get; set; }

    [JsonPropertyName("expectedResult")]
    public string? ExpectedResult { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("steps")]
    public List<GeneratedStep> Steps { get; set; } = [];
}

public sealed class GeneratedStep
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("expectedResult")]
    public string? ExpectedResult { get; set; }
}
