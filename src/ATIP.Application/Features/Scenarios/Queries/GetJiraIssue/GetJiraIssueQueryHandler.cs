using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Models;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Queries.GetJiraIssue;

public sealed class GetJiraIssueQueryHandler : IRequestHandler<GetJiraIssueQuery, JiraIssue>
{
    private readonly IJiraClient _jira;

    public GetJiraIssueQueryHandler(IJiraClient jira) => _jira = jira;

    public Task<JiraIssue> Handle(GetJiraIssueQuery request, CancellationToken cancellationToken) =>
        _jira.GetIssueAsync(request.IssueKey, cancellationToken);
}
