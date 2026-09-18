using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;

namespace ATIP.Application.Engine.Model;

/// <summary>What happened when a driver ran one <see cref="TestAction"/>.</summary>
/// <remarks>
/// <see cref="Performed"/> and <see cref="Passed"/> are deliberately separate. An engine that
/// conflates them reports a click that landed on a dead control as a pass — the mistake this
/// codebase has already had to fix twice. "The driver did the thing" and "the thing had the
/// intended effect" are different claims and are recorded as such.
/// </remarks>
public sealed record ActionResult
{
    /// <summary>True when the driver carried the action out (element found, request sent, query ran).</summary>
    public required bool Performed { get; init; }

    /// <summary>
    /// For assertions: whether the condition held. For non-assertions it mirrors
    /// <see cref="Performed"/> — the action itself is the only claim being made.
    /// </summary>
    public bool Passed { get; init; } = true;

    /// <summary>Human-readable outcome, shown in the run log and the step detail.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// The action's output: element text, HTTP response body, the scalar a query returned. This is
    /// what <see cref="TestAction.SaveAs"/> stores and what later steps interpolate.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>Per-condition breakdown when the action evaluated an assertion.</summary>
    public IReadOnlyList<StepCheckResult> Checks { get; init; } = [];

    /// <summary>
    /// The locator that actually resolved the element. Fed back into the store so the ranking
    /// self-tunes and the next run tries the winner first.
    /// </summary>
    public LocatorCandidate? ResolvedWith { get; init; }

    /// <summary>True when the original locator missed and a fallback (or the AI) found the element.</summary>
    public bool Healed { get; init; }

    /// <summary>
    /// Set when the action could not run for a structural reason — no driver for the platform, the
    /// driver does not support the kind, a connection is missing. Distinct from a test failure:
    /// the product under test is not at fault, the configuration is.
    /// </summary>
    public string? Unsupported { get; init; }

    public static ActionResult Ok(string detail, string? output = null, LocatorCandidate? resolvedWith = null, bool healed = false) =>
        new() { Performed = true, Passed = true, Detail = detail, Output = output, ResolvedWith = resolvedWith, Healed = healed };

    public static ActionResult Fail(string detail) =>
        new() { Performed = false, Passed = false, Detail = detail };

    /// <summary>The action ran, but the condition it was asserting did not hold.</summary>
    public static ActionResult Assertion(bool passed, string detail, IReadOnlyList<StepCheckResult> checks) =>
        new() { Performed = true, Passed = passed, Detail = detail, Checks = checks };

    public static ActionResult NotSupported(string reason) =>
        new() { Performed = false, Passed = false, Detail = reason, Unsupported = reason };
}

/// <summary>The aggregate verdict for a whole step (one or more actions).</summary>
public sealed record StepExecutionResult
{
    public required StepRunStatus Status { get; init; }

    public required string Detail { get; init; }

    public IReadOnlyList<StepCheckResult> Checks { get; init; } = [];

    public IReadOnlyList<ActionResult> Actions { get; init; } = [];
}
