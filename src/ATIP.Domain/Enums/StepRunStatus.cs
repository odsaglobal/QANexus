namespace ATIP.Domain.Enums;

/// <summary>Outcome of executing a single scenario step during a scenario exploration run.</summary>
public enum StepRunStatus
{
    Passed = 0,
    Healed = 1,
    Failed = 2,
    Skipped = 3
}
