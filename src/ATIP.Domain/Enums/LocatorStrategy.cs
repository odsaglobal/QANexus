namespace ATIP.Domain.Enums;

/// <summary>
/// How an element is addressed. Values 0-8 are the original web strategies; 9+ were added for the
/// generic engine (semantic web locators and the native-mobile selector dialects). Persisted as
/// strings, so the numeric values are informational only.
/// </summary>
public enum LocatorStrategy
{
    CSS = 0,
    XPath = 1,
    ARIA = 2,
    Role = 3,
    Text = 4,
    Placeholder = 5,
    DataAttribute = 6,
    NearbyLabel = 7,
    Visual = 8,

    /// <summary>A dedicated test hook: data-test / data-testid / data-qa. The most stable web locator.</summary>
    TestId = 9,

    /// <summary>The element's form label text.</summary>
    Label = 10,

    /// <summary>An image's alt text.</summary>
    AltText = 11,

    /// <summary>The title attribute.</summary>
    Title = 12,

    /// <summary>Native mobile accessibility id (content-desc on Android, name on iOS).</summary>
    AccessibilityId = 13,

    /// <summary>Android UiAutomator2 selector expression.</summary>
    AndroidUiAutomator = 14,

    /// <summary>iOS XCUITest class-chain expression.</summary>
    IosClassChain = 15,

    /// <summary>iOS XCUITest NSPredicate expression.</summary>
    IosPredicate = 16
}
