using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Explorer.Dtos;

public sealed record DiscoveredPageDto
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Path { get; init; }
    public string? Title { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ScreenshotPath { get; init; }
    public bool HasAccessibilityTree { get; init; }
    public int DepthFromRoot { get; init; }
    public string? DiscoveredFromUrl { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static DiscoveredPageDto FromEntity(DiscoveredPage p) => new()
    {
        Id = p.Id,
        SessionId = p.SessionId,
        Name = p.Name,
        Url = p.Url,
        Path = p.Path,
        Title = p.Title,
        HttpStatusCode = p.HttpStatusCode,
        ScreenshotPath = p.ScreenshotPath,
        HasAccessibilityTree = !string.IsNullOrEmpty(p.AccessibilityTreeJson),
        DepthFromRoot = p.DepthFromRoot,
        DiscoveredFromUrl = p.DiscoveredFromUrl,
        CreatedAtUtc = p.CreatedAtUtc,
    };
}

public sealed record DiscoveredPageDetailDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string? Title { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? DomSnapshotPath { get; init; }
    public string? AccessibilityTreeJson { get; init; }

    public static DiscoveredPageDetailDto FromEntity(DiscoveredPage p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Url = p.Url,
        Title = p.Title,
        ScreenshotPath = p.ScreenshotPath,
        DomSnapshotPath = p.DomSnapshotPath,
        AccessibilityTreeJson = p.AccessibilityTreeJson,
    };
}
