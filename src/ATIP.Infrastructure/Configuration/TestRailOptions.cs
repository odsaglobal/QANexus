namespace ATIP.Infrastructure.Configuration;

public sealed class TestRailOptions
{
    public const string SectionName = "TestRail";

    /// <summary>Base URL of the TestRail instance, e.g. https://yourcompany.testrail.io.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>TestRail API user (usually email).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>TestRail API key/password.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(ApiKey);
}
