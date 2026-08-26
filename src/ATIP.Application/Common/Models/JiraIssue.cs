namespace ATIP.Application.Common.Models;

/// <summary>Details fetched for a Jira issue, used to reference/prefill a scenario.</summary>
public sealed record JiraIssue
{
    public required string Key { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    public string? Status { get; init; }

    public string? IssueType { get; init; }

    public string? Priority { get; init; }

    /// <summary>Browser URL to open the issue in Jira.</summary>
    public required string Url { get; init; }
}
