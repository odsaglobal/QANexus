using ATIP.Application.Common.Models;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Queries.GetJiraIssue;

/// <summary>Fetches a Jira issue's details by key (e.g. PROJ-123) for referencing on a scenario.</summary>
public sealed record GetJiraIssueQuery(string IssueKey) : IRequest<JiraIssue>;
