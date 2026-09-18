namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// One atomic QA condition derived from a step/scenario's expected result and evaluated against the
/// live page (e.g. "the total product count does not exceed 10000", "results are sorted price high→low",
/// "an error message is shown"). A verdict is the aggregate of all its checks.
/// </summary>
public sealed class QaCheck
{
    /// <summary>The concrete condition being verified, in plain language.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this individual condition is satisfied by the current page.</summary>
    public bool Passed { get; set; }

    /// <summary>Concrete evidence cited from the page (a number, text, URL) — or why the check failed.</summary>
    public string? Evidence { get; set; }
}

/// <summary>
/// Structured verdict for whether a test's expected result is actually satisfied, broken down into the
/// individual QA conditions it implies. Overall <see cref="Satisfied"/> is true only when every check passes.
/// </summary>
public sealed class QaVerdict
{
    public bool Satisfied { get; set; }

    /// <summary>One-sentence overall summary of the verdict.</summary>
    public string? Summary { get; set; }

    /// <summary>
    /// What the page ACTUALLY showed, in plain language with concrete values. Deliberately separate from
    /// <see cref="Summary"/>: a report already states the expectation, so repeating it inside the failure
    /// message buries the one thing the reader needs — what happened instead.
    /// </summary>
    public string? Actual { get; set; }

    /// <summary>Per-condition breakdown so testers see exactly what was validated.</summary>
    public List<QaCheck> Checks { get; set; } = new();
}
