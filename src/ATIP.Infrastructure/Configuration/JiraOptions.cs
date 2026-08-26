namespace ATIP.Infrastructure.Configuration;

/// <summary>Options for the Jira Cloud REST integration, bound from configuration section "Jira".</summary>
public sealed class JiraOptions
{
    public const string SectionName = "Jira";

    /// <summary>Jira Cloud site base URL, e.g. <c>https://your-domain.atlassian.net</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Atlassian account email used with the API token for Basic auth.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Atlassian API token (id.atlassian.com → Security → API tokens).</summary>
    public string ApiToken { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        && !BaseUrl.Contains('<', StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(ApiToken);
}
