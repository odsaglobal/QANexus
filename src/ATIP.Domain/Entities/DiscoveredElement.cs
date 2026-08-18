using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// A UI element discovered on a page. Every element carries multiple locator strategies so
/// the execution engine and self-healing engine can use whichever strategy still resolves,
/// even after the application changes.
/// </summary>
public class DiscoveredElement : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid PageId { get; set; }

    public DiscoveredPage Page { get; set; } = null!;

    public string? Name { get; set; }

    public string? Role { get; set; }

    public string? AriaLabel { get; set; }

    public string? TextContent { get; set; }

    public string? Placeholder { get; set; }

    public string? DataTestId { get; set; }

    /// <summary>Nearby visible text labels used as semantic context for self-healing.</summary>
    public string? NearbyLabelsJson { get; set; }

    public string? DomPath { get; set; }

    /// <summary>Bounding box {x,y,w,h} as JSON.</summary>
    public string? BoundingBoxJson { get; set; }

    /// <summary>Storage path to a cropped element screenshot.</summary>
    public string? ScreenshotPath { get; set; }

    /// <summary>LLM-generated natural-language description for visual/semantic locating.</summary>
    public string? AiDescription { get; set; }

    public bool IsInteractive { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public double ConfidenceScore { get; set; } = 1.0;

    public DateTimeOffset? LastVerifiedAt { get; set; }

    public int ElementVersion { get; set; } = 1;

    public ICollection<ElementLocator> Locators { get; set; } = new List<ElementLocator>();
}
