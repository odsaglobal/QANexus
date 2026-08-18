namespace ATIP.Application.Features.KnowledgeGraph.Dtos;

/// <summary>A node/edge graph of a project's knowledge, consumed by the React Flow visualization.</summary>
public sealed record KnowledgeGraphDto
{
    public required IReadOnlyList<GraphNodeDto> Nodes { get; init; }
    public required IReadOnlyList<GraphEdgeDto> Edges { get; init; }
}

/// <summary>
/// A graph node. <see cref="Type"/> is one of: project, module, feature, scenario.
/// The client maps type to colour/shape.
/// </summary>
public sealed record GraphNodeDto
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Label { get; init; }
    public string? Sublabel { get; init; }
}

public sealed record GraphEdgeDto
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
}
