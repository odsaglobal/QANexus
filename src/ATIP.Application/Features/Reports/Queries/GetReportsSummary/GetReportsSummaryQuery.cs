using System.Globalization;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Reports.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Reports.Queries.GetReportsSummary;

/// <summary>Tenant-wide quality report (scoped automatically by global query filters).</summary>
public sealed record GetReportsSummaryQuery : IRequest<ReportsSummaryDto>;

public sealed class GetReportsSummaryQueryHandler
    : IRequestHandler<GetReportsSummaryQuery, ReportsSummaryDto>
{
    private const int TrendWeeks = 6;

    private readonly IApplicationDbContext _db;

    public GetReportsSummaryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ReportsSummaryDto> Handle(
        GetReportsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var sessions = await _db.ExplorationSessions
            .AsNoTracking()
            .Select(s => new { s.Status, s.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var totalRuns = sessions.Count;
        var completedRuns = sessions.Count(s => s.Status == ExplorationStatus.Completed);
        var failedRuns = sessions.Count(s => s.Status == ExplorationStatus.Failed);
        var runningRuns = sessions.Count(s =>
            s.Status == ExplorationStatus.Pending || s.Status == ExplorationStatus.Running);

        var stepResults = await _db.ScenarioStepResults
            .AsNoTracking()
            .Select(r => new { r.Status, r.ScenarioId, r.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var stepsPassed = stepResults.Count(r => r.Status == StepRunStatus.Passed);
        var stepsHealed = stepResults.Count(r => r.Status == StepRunStatus.Healed);
        var stepsFailed = stepResults.Count(r => r.Status == StepRunStatus.Failed);
        var evaluated = stepsPassed + stepsHealed + stepsFailed;
        var stepPassRate = evaluated == 0 ? 0 : (int)Math.Round((stepsPassed + stepsHealed) * 100.0 / evaluated);

        var scenarioMix = await _db.Scenarios
            .AsNoTracking()
            .GroupBy(s => s.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var mix = scenarioMix
            .OrderByDescending(x => x.Count)
            .Select(x => new ScenarioTypeSlice(x.Type.ToString(), x.Count))
            .ToList();

        // Weekly execution trend (last N ISO weeks) from step-level pass/fail.
        var now = DateTimeOffset.UtcNow;
        var weekly = new List<WeeklyTrendPoint>();
        for (var i = TrendWeeks - 1; i >= 0; i--)
        {
            var weekStart = StartOfWeek(now.AddDays(-7 * i));
            var weekEnd = weekStart.AddDays(7);
            var inWeek = stepResults.Where(r => r.CreatedAtUtc >= weekStart && r.CreatedAtUtc < weekEnd).ToList();
            var passed = inWeek.Count(r => r.Status == StepRunStatus.Passed || r.Status == StepRunStatus.Healed);
            var failed = inWeek.Count(r => r.Status == StepRunStatus.Failed);
            var label = $"W{ISOWeek.GetWeekOfYear(weekStart.UtcDateTime)}";
            weekly.Add(new WeeklyTrendPoint(label, passed, failed));
        }

        // Top scenarios by failed steps.
        var failedByScenario = stepResults
            .Where(r => r.Status == StepRunStatus.Failed)
            .GroupBy(r => r.ScenarioId)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var scenarioIds = failedByScenario.Select(x => x.Key).ToList();
        var titles = await _db.Scenarios
            .AsNoTracking()
            .Where(s => scenarioIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(cancellationToken);
        var titleMap = titles.ToDictionary(t => t.Id, t => t.Title);

        var topFailing = failedByScenario
            .Select(x => new FailingScenario(
                titleMap.TryGetValue(x.Key, out var title) ? title : "Unknown scenario",
                x.Count))
            .ToList();

        // Recent runs (last 8 sessions tenant-wide) with a human label + project name.
        var recentSessions = await _db.ExplorationSessions
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(8)
            .Select(s => new
            {
                s.Id,
                s.ProjectId,
                s.ScenarioId,
                s.SuiteId,
                s.RecordedScenarioId,
                s.Prompt,
                s.Status,
                s.StartedAtUtc,
                s.CompletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var recentScenarioIds = recentSessions
            .SelectMany(s => new[] { s.ScenarioId, s.RecordedScenarioId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var recentSuiteIds = recentSessions
            .Where(s => s.SuiteId.HasValue)
            .Select(s => s.SuiteId!.Value)
            .Distinct()
            .ToList();
        var recentProjectIds = recentSessions.Select(s => s.ProjectId).Distinct().ToList();

        var recentScenarioTitles = await _db.Scenarios
            .AsNoTracking()
            .Where(s => recentScenarioIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(cancellationToken);
        var recentScenarioMap = recentScenarioTitles.ToDictionary(t => t.Id, t => t.Title);

        var recentSuiteNames = await _db.TestSuites
            .AsNoTracking()
            .Where(s => recentSuiteIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);
        var recentSuiteMap = recentSuiteNames.ToDictionary(t => t.Id, t => t.Name);

        var projectNames = await _db.Projects
            .AsNoTracking()
            .Where(p => recentProjectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        var projectMap = projectNames.ToDictionary(p => p.Id, p => p.Name);

        var recentRuns = recentSessions.Select(s =>
        {
            string label;
            if (s.SuiteId.HasValue && recentSuiteMap.TryGetValue(s.SuiteId.Value, out var suiteName))
                label = $"Suite: {suiteName}";
            else if (s.ScenarioId.HasValue && recentScenarioMap.TryGetValue(s.ScenarioId.Value, out var scTitle))
                label = scTitle;
            else if (s.RecordedScenarioId.HasValue && recentScenarioMap.TryGetValue(s.RecordedScenarioId.Value, out var recTitle))
                label = recTitle;
            else if (!string.IsNullOrWhiteSpace(s.Prompt))
                label = s.Prompt!;
            else
                label = "Application exploration";

            int? duration = s.StartedAtUtc.HasValue && s.CompletedAtUtc.HasValue
                ? (int)Math.Max(0, (s.CompletedAtUtc.Value - s.StartedAtUtc.Value).TotalSeconds)
                : null;

            return new RecentRun(
                s.Id,
                label,
                projectMap.TryGetValue(s.ProjectId, out var pName) ? pName : "Project",
                s.Status.ToString(),
                s.StartedAtUtc,
                duration);
        }).ToList();

        return new ReportsSummaryDto
        {
            TotalRuns = totalRuns,
            CompletedRuns = completedRuns,
            FailedRuns = failedRuns,
            RunningRuns = runningRuns,
            StepsPassed = stepsPassed,
            StepsHealed = stepsHealed,
            StepsFailed = stepsFailed,
            StepPassRate = stepPassRate,
            ScenarioMix = mix,
            WeeklyTrend = weekly,
            TopFailingScenarios = topFailing,
            RecentRuns = recentRuns,
        };
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset date)
    {
        var diff = (7 + (int)date.UtcDateTime.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = date.UtcDateTime.Date.AddDays(-diff);
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }
}
