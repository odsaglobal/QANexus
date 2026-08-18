using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Explorer.Dtos;

public sealed record DiscoveredElementDto
{
    public required Guid Id { get; init; }
    public required Guid PageId { get; init; }
    public string? Name { get; init; }
    public string? Role { get; init; }
    public string? AriaLabel { get; init; }
    public string? TextContent { get; init; }
    public string? Placeholder { get; init; }
    public string? DataTestId { get; init; }
    public string? BoundingBoxJson { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? AiDescription { get; init; }
    public bool IsInteractive { get; init; }
    public bool IsVisible { get; init; }
    public double ConfidenceScore { get; init; }
    public required IReadOnlyList<ElementLocatorDto> Locators { get; init; }

    public static DiscoveredElementDto FromEntity(DiscoveredElement e) => new()
    {
        Id = e.Id,
        PageId = e.PageId,
        Name = e.Name,
        Role = e.Role,
        AriaLabel = e.AriaLabel,
        TextContent = e.TextContent,
        Placeholder = e.Placeholder,
        DataTestId = e.DataTestId,
        BoundingBoxJson = e.BoundingBoxJson,
        ScreenshotPath = e.ScreenshotPath,
        AiDescription = e.AiDescription,
        IsInteractive = e.IsInteractive,
        IsVisible = e.IsVisible,
        ConfidenceScore = e.ConfidenceScore,
        Locators = e.Locators
            .OrderByDescending(l => l.IsPrimary)
            .ThenByDescending(l => l.ConfidenceScore)
            .Select(ElementLocatorDto.FromEntity)
            .ToList(),
    };
}

public sealed record ElementLocatorDto
{
    public required Guid Id { get; init; }
    public required string Strategy { get; init; }
    public required string Value { get; init; }
    public bool IsPrimary { get; init; }
    public double ConfidenceScore { get; init; }
    public bool IsVerified { get; init; }
    public int FailureCount { get; init; }

    public static ElementLocatorDto FromEntity(ElementLocator l) => new()
    {
        Id = l.Id,
        Strategy = l.Strategy.ToString(),
        Value = l.Value,
        IsPrimary = l.IsPrimary,
        ConfidenceScore = l.ConfidenceScore,
        IsVerified = l.IsVerified,
        FailureCount = l.FailureCount,
    };
}
