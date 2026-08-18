using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.GetLatestScenarioRun;

public sealed class GetLatestScenarioRunQueryHandler
    : IRequestHandler<GetLatestScenarioRunQuery, ScenarioRunDto?>
{
    private readonly IApplicationDbContext _db;

    public GetLatestScenarioRunQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScenarioRunDto?> Handle(GetLatestScenarioRunQuery request, CancellationToken cancellationToken)
    {
        var session = await _db.ExplorationSessions
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId && s.ScenarioId == request.ScenarioId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        var results = await _db.ScenarioStepResults
            .AsNoTracking()
            .Where(r => r.SessionId == session.Id)
            .OrderBy(r => r.StepOrder)
            .ToListAsync(cancellationToken);

        var passed = results.Count(r => r.Status == StepRunStatus.Passed);
        var healed = results.Count(r => r.Status == StepRunStatus.Healed);
        var failed = results.Count(r => r.Status == StepRunStatus.Failed);

        var outcome = failed > 0 ? "Failed" : healed > 0 ? "Passed with healing" : "Passed";

        return new ScenarioRunDto
        {
            SessionId = session.Id,
            ScenarioId = request.ScenarioId,
            Status = session.Status.ToString(),
            Outcome = results.Count == 0 ? session.Status.ToString() : outcome,
            PassedSteps = passed,
            HealedSteps = healed,
            FailedSteps = failed,
            StartedAtUtc = session.StartedAtUtc,
            CompletedAtUtc = session.CompletedAtUtc,
            Steps = results.Select(ScenarioStepResultDto.FromEntity).ToList(),
        };
    }
}
