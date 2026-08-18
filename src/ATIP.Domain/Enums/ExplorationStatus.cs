namespace ATIP.Domain.Enums;

/// <summary>Lifecycle of a browser-based application exploration session.</summary>
public enum ExplorationStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
