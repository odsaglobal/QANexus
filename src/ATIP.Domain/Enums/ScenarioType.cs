namespace ATIP.Domain.Enums;

/// <summary>Category of a generated test scenario.</summary>
public enum ScenarioType
{
    Positive = 0,
    Negative = 1,
    Boundary = 2,
    Regression = 3,
    Smoke = 4,
    Sanity = 5,
    Accessibility = 6,
    Security = 7,
    Api = 8,
    CrossBrowser = 9,
    Performance = 10
}
