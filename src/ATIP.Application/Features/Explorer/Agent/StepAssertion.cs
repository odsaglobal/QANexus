namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// One machine-checkable assertion compiled from a step's English expected result.
/// <para>
/// The point of compiling is that an English expectation can only be judged by a language model,
/// whereas <c>{ Source, Selector, Operator, Expected }</c> can be evaluated by reading the DOM and
/// comparing values. The AI is used once, at authoring time, to WRITE the assertion; it is never
/// asked to JUDGE it. Every run afterwards is deterministic, free and reproducible.
/// </para>
/// </summary>
public sealed class StepAssertion
{
    /// <summary>
    /// The author's own wording for this assertion, shown as a leaf in the report. Kept verbatim so a
    /// tester reads their sentence back rather than the selector it compiled to.
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>Where the observed value comes from: text | value | count | url | title.</summary>
    public string Source { get; set; } = "text";

    /// <summary>
    /// CSS selector for the element(s) to read. Ignored for <c>url</c> and <c>title</c>. Multiple
    /// matches are read in document order, which is what the ordering operators compare.
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// How the observed value is compared: equals | not_equals | contains | not_contains | matches |
    /// exists | not_exists | sorted_asc | sorted_desc | first_is_max | first_is_min | gte | lte |
    /// count_equals.
    /// </summary>
    public string Operator { get; set; } = "contains";

    /// <summary>The value to compare against. Unused by the ordering and existence operators.</summary>
    public string? Expected { get; set; }
}

/// <summary>
/// Outcome of evaluating one <see cref="StepAssertion"/> against the live page. Carries the concrete
/// observed value so the report can show expected and actual side by side instead of a sentence.
/// </summary>
public sealed class StepCheckResult
{
    public string Label { get; set; } = "";

    public bool Passed { get; set; }

    /// <summary>Human-readable statement of what was required, e.g. "sorted high to low".</summary>
    public string? Expected { get; set; }

    /// <summary>Concrete value(s) read from the page, e.g. "₹14,989, ₹1,29,900 …".</summary>
    public string? Actual { get; set; }

    /// <summary>
    /// True when the assertion could not be evaluated at all (selector matched nothing), as opposed to
    /// being evaluated and found false. Both fail the step, but only this one means "fix the test".
    /// </summary>
    public bool Unresolved { get; set; }
}
