using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A named element in the project's object repository — the durable identity of "the Login
/// button" independent of how it happens to be addressable today.
/// </summary>
/// <remarks>
/// This is deliberately separate from <see cref="DiscoveredElement"/>. A discovered element
/// belongs to one crawled page in one exploration session and is therefore disposable; a
/// <see cref="UiElement"/> is project-scoped, survives every run, and is what steps bind to.
/// Keeping the two apart is what makes self-healing cumulative: each run adds evidence about
/// which <see cref="UiElementLocator"/> still works instead of starting from a blank page.
/// </remarks>
public class UiElement : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>
    /// Stable, human-authored identifier used by steps to reference this element, e.g.
    /// <c>checkout.placeOrderButton</c>. Unique per project so a step can bind by name rather
    /// than by a database id that means nothing in an exported scenario.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>Display name, e.g. "Place order".</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Which automation surface the element lives on (web page vs mobile screen).</summary>
    public TestPlatform Platform { get; set; } = TestPlatform.Web;

    /// <summary>
    /// Optional URL (or URL pattern) of the screen the element belongs to. Used to prefer the
    /// right candidate when the same key exists on more than one screen, never as a hard filter.
    /// </summary>
    public string? ScreenUrlPattern { get; set; }

    /// <summary>ARIA role of the element when known — a strong disambiguator while healing.</summary>
    public string? Role { get; set; }

    /// <summary>Last accessible name / visible text seen for this element.</summary>
    public string? AccessibleName { get; set; }

    public DateTimeOffset? LastResolvedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<UiElementLocator> Locators { get; set; } = new List<UiElementLocator>();
}
