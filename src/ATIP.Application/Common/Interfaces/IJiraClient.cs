using ATIP.Application.Common.Models;

namespace ATIP.Application.Common.Interfaces;

/// <summary>Fetches issue details from Jira Cloud.</summary>
public interface IJiraClient
{
    Task<JiraIssue> GetIssueAsync(string issueKey, CancellationToken cancellationToken = default);
}
