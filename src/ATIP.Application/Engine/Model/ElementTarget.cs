using ATIP.Domain.Enums;

namespace ATIP.Application.Engine.Model;

/// <summary>
/// How an action finds the thing it acts on. A target is either a reference into the project's
/// object repository (<see cref="ElementKey"/>), an explicit list of locator candidates, or a
/// plain human descriptor the AI can resolve — in that order of preference.
/// </summary>
/// <remarks>
/// Keeping all three in one type is what lets a step survive its own maturity curve: the AI
/// records it as a <see cref="Descriptor"/>, the first successful run promotes the winning locator
/// into <see cref="Candidates"/>, and once the element is named in the repository the step becomes
/// a stable <see cref="ElementKey"/> reference that no longer depends on the page's wording.
/// </remarks>
public sealed class ElementTarget
{
    /// <summary>Key of a <c>UiElement</c> in the project's object repository, e.g. <c>login.submit</c>.</summary>
    public string? ElementKey { get; set; }

    /// <summary>Inline locator candidates, tried in order when no repository entry applies.</summary>
    public List<LocatorCandidate> Candidates { get; set; } = [];

    /// <summary>
    /// Human-meaningful description ("the Place order button"). Last resort: resolved by the
    /// accessible-name / visible-text matcher, and by the AI healer if that misses.
    /// </summary>
    public string? Descriptor { get; set; }

    /// <summary>
    /// Zero-based index when the locator legitimately matches several elements (the third row's
    /// Delete button). Null means "must match exactly one" — ambiguity is reported, not guessed.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>True when nothing at all was supplied to locate an element.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(ElementKey)
        && string.IsNullOrWhiteSpace(Descriptor)
        && Candidates.Count == 0;

    public static ElementTarget FromDescriptor(string descriptor) => new() { Descriptor = descriptor };

    public override string ToString() =>
        ElementKey
        ?? Descriptor
        ?? (Candidates.Count > 0 ? $"{Candidates[0].Strategy}={Candidates[0].Value}" : "(no target)");
}

/// <summary>One concrete way to address an element, with the confidence the engine currently has in it.</summary>
public sealed class LocatorCandidate
{
    public LocatorStrategy Strategy { get; set; } = LocatorStrategy.CSS;

    public required string Value { get; set; }

    /// <summary>Lower runs first.</summary>
    public int Rank { get; set; }

    /// <summary>Id of the persisted <c>UiElementLocator</c> this came from, when it came from the store.</summary>
    public Guid? LocatorId { get; set; }

    /// <summary>
    /// True when the value only addresses the element during this browser session — a snapshot ref
    /// stamped onto the DOM, for instance. Volatile candidates may be used to act, but must never
    /// be written to the object repository: they are meaningless on the next page load.
    /// </summary>
    public bool Volatile { get; set; }

    public static LocatorCandidate Of(LocatorStrategy strategy, string value, int rank = 0) =>
        new() { Strategy = strategy, Value = value, Rank = rank };
}
