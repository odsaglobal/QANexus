using ATIP.Domain.Enums;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Classifies the outcome of a single scenario step execution. This keeps the persisted
/// result semantics precise and consistent across AI-assisted and fallback execution paths.
/// </summary>
public static class ScenarioStepOutcomeResolver
{
    public static StepRunStatus Resolve(bool resolved, int fallbackIndex, string? detail = null)
    {
        if (!resolved)
        {
            return StepRunStatus.Failed;
        }

        return fallbackIndex > 0 ? StepRunStatus.Healed : StepRunStatus.Passed;
    }

    public static StepRunStatus ResolveAssertion(bool resolved, bool assertionMatched, int fallbackIndex)
    {
        if (!resolved || !assertionMatched)
        {
            return StepRunStatus.Failed;
        }

        return fallbackIndex > 0 ? StepRunStatus.Healed : StepRunStatus.Passed;
    }
}
