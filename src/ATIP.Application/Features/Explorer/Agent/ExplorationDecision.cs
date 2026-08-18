using System.Text.Json.Serialization;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>Shape of the JSON the LLM returns when deciding what to interact with on a page.</summary>
public sealed class ExplorationDecision
{
    [JsonPropertyName("shouldInteract")]
    public bool ShouldInteract { get; set; }

    [JsonPropertyName("interactions")]
    public List<PlannedInteraction> Interactions { get; set; } = [];
}

public sealed class PlannedInteraction
{
    [JsonPropertyName("selector")]
    public string Selector { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
