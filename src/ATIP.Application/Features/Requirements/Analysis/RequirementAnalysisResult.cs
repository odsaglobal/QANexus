using System.Text.Json.Serialization;

namespace ATIP.Application.Features.Requirements.Analysis;

/// <summary>
/// Strongly-typed shape of the JSON the LLM is asked to return when analyzing a requirement
/// document. Kept deliberately simple so both the live model and the offline mock can produce it.
/// </summary>
public sealed class RequirementAnalysisResult
{
    [JsonPropertyName("modules")]
    public List<AnalyzedModule> Modules { get; set; } = [];
}

public sealed class AnalyzedModule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("features")]
    public List<AnalyzedFeature> Features { get; set; } = [];
}

public sealed class AnalyzedFeature
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("businessRules")]
    public List<string> BusinessRules { get; set; } = [];

    [JsonPropertyName("userStories")]
    public List<AnalyzedUserStory> UserStories { get; set; } = [];
}

public sealed class AnalyzedUserStory
{
    [JsonPropertyName("asA")]
    public string AsA { get; set; } = string.Empty;

    [JsonPropertyName("iWant")]
    public string IWant { get; set; } = string.Empty;

    [JsonPropertyName("soThat")]
    public string? SoThat { get; set; }

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = [];
}
