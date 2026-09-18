namespace ATIP.Infrastructure.Engine.Mobile;

/// <summary>Where the Appium/WebDriver server is and what session to ask it for.</summary>
public sealed class MobileDriverOptions
{
    public const string SectionName = "Mobile";

    /// <summary>Base URL of the Appium server, e.g. <c>http://localhost:4723</c>.</summary>
    public string ServerUrl { get; set; } = "http://localhost:4723";

    /// <summary>
    /// W3C capabilities as a JSON object (<c>platformName</c>, <c>appium:automationName</c>,
    /// <c>appium:app</c>, …). Kept as raw JSON because the capability set is vendor-specific and
    /// changes far faster than any typed model of it could.
    /// </summary>
    public string CapabilitiesJson { get; set; } = """
        { "platformName": "Android", "appium:automationName": "UiAutomator2" }
        """;

    public int RequestTimeoutSeconds { get; set; } = 60;
}
