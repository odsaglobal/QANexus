using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// A page (URL) discovered and captured during an exploration session. Stores the full
/// accessibility tree (inline when ≤ 500 KB), a DOM snapshot path and a screenshot path
/// so the AI and the locator engine can reason about the page without re-visiting it.
/// </summary>
public class DiscoveredPage : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid SessionId { get; set; }

    public ExplorationSession Session { get; set; } = null!;

    /// <summary>Human-friendly page name derived from the title or path.</summary>
    public required string Name { get; set; }

    public required string Url { get; set; }

    /// <summary>Relative path component of the URL.</summary>
    public required string Path { get; set; }

    public string? Title { get; set; }

    public int? HttpStatusCode { get; set; }

    /// <summary>Accessibility tree JSON stored inline (≤ 500 KB) or null when too large.</summary>
    public string? AccessibilityTreeJson { get; set; }

    /// <summary>Storage path to the DOM HTML snapshot.</summary>
    public string? DomSnapshotPath { get; set; }

    /// <summary>Storage path to the full-page PNG screenshot.</summary>
    public string? ScreenshotPath { get; set; }

    public bool IsExplored { get; set; }

    /// <summary>Hops from the seed URL to this page.</summary>
    public int DepthFromRoot { get; set; }

    public string? DiscoveredFromUrl { get; set; }
}
