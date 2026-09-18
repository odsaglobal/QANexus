using ATIP.Application.Engine.Model;
using ATIP.Domain.Enums;

namespace ATIP.Application.Engine.Contracts;

/// <summary>
/// The persistent memory behind self-healing: which locators exist for an element, which ones have
/// been working lately, and which have gone stale.
/// </summary>
/// <remarks>
/// Before this existed, healing started from scratch on every failure — the AI was handed the page
/// HTML and asked to invent a selector again, run after run, with no recollection that the same
/// element had already been re-found yesterday. Persisting outcomes turns that into a ratchet: the
/// engine tries the historical winner first and only pays for AI healing when the page genuinely
/// changed.
/// </remarks>
public interface ILocatorRepository
{
    /// <summary>
    /// Locator candidates for a repository element, best first: non-quarantined, ordered by rank
    /// and then by observed success. Empty if the key is unknown.
    /// </summary>
    /// <remarks>
    /// The tenant is passed explicitly rather than inferred from the ambient user because runs
    /// execute on a background service with no HTTP principal, where the global tenant filter is
    /// bypassed. Making it a parameter keeps isolation enforced instead of silently absent.
    /// </remarks>
    Task<IReadOnlyList<LocatorCandidate>> GetCandidatesAsync(
        Guid tenantId,
        Guid projectId,
        string elementKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a locator resolved successfully: clears its consecutive-failure count, lifts
    /// its rank, and lets it out of quarantine.
    /// </summary>
    Task RecordSuccessAsync(Guid locatorId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a miss. Quarantines the locator once it has failed enough times in a row that it is
    /// costing a timeout on every run for nothing.
    /// </summary>
    Task RecordFailureAsync(Guid locatorId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a locator the healer just proved works, creating the element if the key is new.
    /// Appends rather than replacing: the old locator may only have failed because the page was in
    /// an unexpected state, and discarding it would lose a working path back.
    /// </summary>
    Task<Guid> UpsertLocatorAsync(
        Guid tenantId,
        Guid projectId,
        string elementKey,
        string elementName,
        TestPlatform platform,
        LocatorStrategy strategy,
        string value,
        LocatorOrigin origin,
        CancellationToken cancellationToken);
}

/// <summary>
/// Turns an <see cref="ElementTarget"/> into the ordered list of locators to try, merging stored
/// history with whatever the action carried inline.
/// </summary>
public interface ILocatorResolver
{
    /// <summary>
    /// Builds the candidate list for a target: stored locators for <see cref="ElementTarget.ElementKey"/>
    /// first (they carry evidence), then the action's inline candidates, de-duplicated.
    /// </summary>
    Task<IReadOnlyList<LocatorCandidate>> ResolveAsync(
        ElementTarget target,
        RunContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reports which candidate won and which lost, so the ranking converges on what actually works.
    /// A winner that did not come from the store is promoted into it, which is what turns a
    /// one-off descriptor match into durable knowledge about the application.
    /// </summary>
    Task ReportOutcomeAsync(
        ElementTarget target,
        IReadOnlyList<LocatorCandidate> attempted,
        LocatorCandidate? winner,
        RunContext context,
        CancellationToken cancellationToken);
}
