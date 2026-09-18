using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// One way to address a <see cref="UiElement"/>, plus the evidence that says whether it still
/// works. The engine keeps several per element and tries them in rank order.
/// </summary>
/// <remarks>
/// <para>
/// The ranking is earned, not declared. <see cref="Rank"/> seeds the order from the strategy's
/// intrinsic stability (a <c>data-testid</c> beats a hashed CSS class), and every resolution
/// feeds <see cref="SuccessCount"/> / <see cref="FailureCount"/> back, so a locator that keeps
/// winning drifts to the front and one that keeps missing is quarantined instead of costing a
/// timeout on every future run.
/// </para>
/// <para>
/// <see cref="FailureCount"/> is a CONSECUTIVE counter: a single success resets it. Otherwise a
/// locator that is merely conditional (an element that is absent on some pages) would accumulate
/// failures forever and eventually be quarantined despite being correct.
/// </para>
/// </remarks>
public class UiElementLocator : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ElementId { get; set; }

    public UiElement Element { get; set; } = null!;

    public LocatorStrategy Strategy { get; set; }

    /// <summary>
    /// The strategy's payload: a CSS selector, an XPath expression, <c>role:name</c>, the exact
    /// visible text, a placeholder, or a mobile accessibility id.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>Lower runs first. Seeded from strategy stability, then adjusted by outcomes.</summary>
    public int Rank { get; set; }

    public LocatorOrigin Origin { get; set; } = LocatorOrigin.Recorded;

    /// <summary>Number of runs in which this locator resolved to exactly one element.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Consecutive failures since the last success. Reset to zero on any success.</summary>
    public int FailureCount { get; set; }

    public DateTimeOffset? LastSucceededAtUtc { get; set; }

    public DateTimeOffset? LastFailedAtUtc { get; set; }

    /// <summary>
    /// Set once a locator has failed too many times in a row. Quarantined locators are skipped
    /// during resolution but kept on the record, because a redeploy can revive them and the
    /// history explains why a step started healing.
    /// </summary>
    public bool IsQuarantined { get; set; }
}
