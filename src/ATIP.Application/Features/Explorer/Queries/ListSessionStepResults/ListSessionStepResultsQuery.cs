using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.ListSessionStepResults;

/// <summary>
/// Every persisted step result for a session, across all scenarios it ran (a single scenario, or every
/// scenario in a suite), in execution order — the data behind the Executions "View" detail. Works for
/// both completed and still-running sessions (unlike the live SignalR stream, which has nothing to
/// replay once a session reaches a terminal state).
/// </summary>
public sealed record ListSessionStepResultsQuery(Guid SessionId) : IRequest<IReadOnlyList<SessionStepResultDto>>;

public sealed class ListSessionStepResultsQueryHandler
    : IRequestHandler<ListSessionStepResultsQuery, IReadOnlyList<SessionStepResultDto>>
{
    private readonly IApplicationDbContext _db;

    public ListSessionStepResultsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SessionStepResultDto>> Handle(
        ListSessionStepResultsQuery request, CancellationToken cancellationToken)
    {
        var results = await _db.ScenarioStepResults
            .AsNoTracking()
            .Where(r => r.SessionId == request.SessionId)
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.StepOrder)
            .ToListAsync(cancellationToken);

        if (results.Count == 0)
        {
            return [];
        }

        var scenarioIds = results.Select(r => r.ScenarioId).Distinct().ToList();
        var titles = await _db.Scenarios
            .AsNoTracking()
            .Where(s => scenarioIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Title })
            .ToDictionaryAsync(s => s.Id, s => s.Title, cancellationToken);

        return results
            .Select(r => SessionStepResultDto.FromEntity(r, titles.GetValueOrDefault(r.ScenarioId, "Scenario")))
            .ToList();
    }
}
