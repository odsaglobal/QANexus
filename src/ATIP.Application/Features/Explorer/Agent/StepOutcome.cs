using ATIP.Domain.Enums;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// The result of running one scenario step: its verdict, the sentence shown next to it, and — when the
/// step's expectation was compiled into assertions — the per-assertion breakdown behind that verdict.
/// <para>
/// The implicit conversion from a <c>(status, detail)</c> tuple exists so the many paths that have no
/// structured checks to report (navigation, healing, locator failures) stay as readable as they were.
/// </para>
/// </summary>
public sealed record StepOutcome(
    StepRunStatus Status,
    string Detail,
    IReadOnlyList<StepCheckResult>? Checks = null)
{
    public static implicit operator StepOutcome((StepRunStatus Status, string Detail) outcome) =>
        new(outcome.Status, outcome.Detail);
}
