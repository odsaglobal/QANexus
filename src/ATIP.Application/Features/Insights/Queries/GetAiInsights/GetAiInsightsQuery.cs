using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Insights.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Insights.Queries.GetAiInsights;

/// <summary>
/// Derives actionable quality findings from real execution history and authoring state.
/// Optionally narrowed to one project; otherwise tenant-wide (scoped by global query filters).
/// </summary>
public sealed record GetAiInsightsQuery(Guid? ProjectId = null) : IRequest<AiInsightsDto>;

/// <summary>
/// Detection is deliberately deterministic rather than LLM-generated. Every finding is a counted
/// fact about the tenant's own data and ships with the observations behind it, so a user can verify
/// it. An LLM asked to "find problems" in the same data would produce plausible prose that nobody
/// can check — which is worse than useless on a quality dashboard.
/// </summary>
public sealed class GetAiInsightsQueryHandler : IRequestHandler<GetAiInsightsQuery, AiInsightsDto>
{
    /// <summary>A step needs this much history before intermittency is distinguishable from noise.</summary>
    private const int MinRunsForFlakyVerdict = 3;

    /// <summary>Consecutive trailing failures that promote a step from "flaky" to "broken".</summary>
    private const int ConsecutiveFailuresForRegression = 2;

    /// <summary>Heals on one step before locator drift is called out.</summary>
    private const int MinHealsForDrift = 2;

    private const int StaleAfterDays = 30;

    private const int MaxEvidenceLines = 5;

    /// <summary>How many run outcomes the evidence's compact sequence line covers.</summary>
    private const int SequenceLength = 12;

    private const int MaxNamesInSummary = 3;

    private readonly IApplicationDbContext _db;

    public GetAiInsightsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AiInsightsDto> Handle(GetAiInsightsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = request.ProjectId;

        var projectNames = (await _db.Projects
                .AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

        var features = await _db.Features
            .AsNoTracking()
            .Select(f => new FeatureRow(f.Id, f.Name, f.ProjectId))
            .ToListAsync(cancellationToken);
        var featureMap = features.ToDictionary(f => f.Id);

        // Scenarios hang off features, not projects, so project scoping has to hop through the feature.
        var scenarioRows = await _db.Scenarios
            .AsNoTracking()
            .Select(s => new { s.Id, s.Title, s.FeatureId })
            .ToListAsync(cancellationToken);

        var scenarios = new Dictionary<Guid, ScenarioMeta>();
        foreach (var row in scenarioRows)
        {
            var projectId = featureMap.TryGetValue(row.FeatureId, out var feature) ? feature.ProjectId : Guid.Empty;
            if (filter is not null && projectId != filter) continue;

            scenarios[row.Id] = new ScenarioMeta(
                row.Id,
                row.Title,
                projectId,
                projectNames.TryGetValue(projectId, out var name) ? name : "Unknown project");
        }

        var steps = (await _db.ScenarioSteps
                .AsNoTracking()
                .Select(s => new StepRow(s.ScenarioId, s.Order, s.Action, s.NeedsReview, s.ReviewReason, s.RecordedActionsJson))
                .ToListAsync(cancellationToken))
            .Where(s => scenarios.ContainsKey(s.ScenarioId))
            .ToList();

        var results = (await _db.ScenarioStepResults
                .AsNoTracking()
                .Select(r => new ResultRow(r.SessionId, r.ScenarioId, r.StepOrder, r.Action, r.Status, r.Detail, r.CreatedAtUtc))
                .ToListAsync(cancellationToken))
            .Where(r => scenarios.ContainsKey(r.ScenarioId))
            .ToList();

        var insights = new List<AiInsightDto>();

        AddExecutionInsights(insights, results, scenarios);
        AddCoverageInsights(insights, results, steps, scenarios, now);
        AddAuthoringInsights(insights, steps, scenarios);
        AddFeatureCoverageInsights(insights, features, scenarioRows.Select(s => s.FeatureId), projectNames, filter);
        await AddRequirementInsightsAsync(insights, projectNames, filter, cancellationToken);
        await AddRunFailureInsightsAsync(insights, projectNames, filter, cancellationToken);

        return new AiInsightsDto
        {
            GeneratedAtUtc = now,
            RunsAnalyzed = results.Select(r => r.SessionId).Distinct().Count(),
            ScenariosAnalyzed = scenarios.Count,
            StepResultsAnalyzed = results.Count,
            Insights = insights
                .OrderBy(i => SeverityRank(i.Severity))
                .ThenByDescending(i => i.LastSeenUtc ?? DateTimeOffset.MinValue)
                .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    /// <summary>
    /// Per-step verdicts from run history: broken steps, intermittent steps, and locator drift.
    /// A step is judged on its own timeline because "the scenario fails" is not actionable —
    /// "step 4 has failed the last three runs" is.
    /// </summary>
    private static void AddExecutionInsights(
        List<AiInsightDto> insights,
        IReadOnlyList<ResultRow> results,
        IReadOnlyDictionary<Guid, ScenarioMeta> scenarios)
    {
        var groups = results
            .Where(r => r.Status != StepRunStatus.Skipped)
            .GroupBy(r => (r.ScenarioId, r.StepOrder));

        foreach (var group in groups)
        {
            var scenario = scenarios[group.Key.ScenarioId];
            var ordered = group.OrderBy(r => r.CreatedAtUtc).ToList();

            var runs = ordered.Count;
            var failures = ordered.Count(r => r.Status == StepRunStatus.Failed);
            var heals = ordered.Count(r => r.Status == StepRunStatus.Healed);
            var last = ordered[^1];
            var action = last.Action;
            var stepLabel = $"Step {group.Key.StepOrder}";

            var trailingFailures = 0;
            for (var i = runs - 1; i >= 0 && ordered[i].Status == StepRunStatus.Failed; i--) trailingFailures++;

            // Pass/fail flips over time. Two or more means the outcome is not a function of the code
            // under test — it is timing, data, or an unstable locator.
            var flips = 0;
            for (var i = 1; i < runs; i++)
            {
                if ((ordered[i].Status == StepRunStatus.Failed) != (ordered[i - 1].Status == StepRunStatus.Failed)) flips++;
            }

            if (trailingFailures >= ConsecutiveFailuresForRegression)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"broken:{scenario.Id}:{group.Key.StepOrder}",
                    Category = "Reliability",
                    Severity = "Critical",
                    Title = $"{stepLabel} fails on every recent run — {scenario.Title}",
                    Summary = $"\"{Truncate(action, 90)}\" has failed the last {trailingFailures} consecutive runs "
                              + $"({failures} of {runs} runs overall). This is a persistent break, not intermittency.",
                    SuggestedAction = string.IsNullOrWhiteSpace(last.Detail)
                        ? "Replay the scenario and inspect the step's screenshot to confirm whether the application changed or the step is stale."
                        : $"Investigate the reported cause: {Truncate(last.Detail!, 160)}",
                    Evidence = BuildTimeline(ordered),
                    ProjectId = scenario.ProjectId,
                    ProjectName = scenario.ProjectName,
                    ScenarioId = scenario.Id,
                    ScenarioTitle = scenario.Title,
                    LastSeenUtc = last.CreatedAtUtc,
                });
            }
            else if (flips >= 2 && runs >= MinRunsForFlakyVerdict)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"flaky:{scenario.Id}:{group.Key.StepOrder}",
                    Category = "Reliability",
                    Severity = "Warning",
                    Title = $"{stepLabel} is flaky — {scenario.Title}",
                    Summary = $"\"{Truncate(action, 90)}\" changed outcome {flips} times across {runs} runs "
                              + $"({failures} failed, {runs - failures} passed). An intermittent step erodes trust in every run it appears in.",
                    SuggestedAction = "Replace implicit timing with an explicit wait on the element or text this step depends on, and confirm it does not rely on data left behind by an earlier run.",
                    Evidence = BuildTimeline(ordered),
                    ProjectId = scenario.ProjectId,
                    ProjectName = scenario.ProjectName,
                    ScenarioId = scenario.Id,
                    ScenarioTitle = scenario.Title,
                    LastSeenUtc = last.CreatedAtUtc,
                });
            }
            else if (trailingFailures == 1 && failures == 1 && runs >= 2)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"regression:{scenario.Id}:{group.Key.StepOrder}",
                    Category = "Reliability",
                    Severity = "Warning",
                    Title = $"{stepLabel} started failing — {scenario.Title}",
                    Summary = $"\"{Truncate(action, 90)}\" passed on its previous {runs - 1} run(s) and failed on the most recent one. "
                              + "A first failure after a clean history usually means something changed.",
                    SuggestedAction = string.IsNullOrWhiteSpace(last.Detail)
                        ? "Re-run the scenario. If it fails again treat it as a regression; if it passes, treat it as flakiness."
                        : $"Re-run to confirm, then investigate: {Truncate(last.Detail!, 160)}",
                    Evidence = BuildTimeline(ordered),
                    ProjectId = scenario.ProjectId,
                    ProjectName = scenario.ProjectName,
                    ScenarioId = scenario.Id,
                    ScenarioTitle = scenario.Title,
                    LastSeenUtc = last.CreatedAtUtc,
                });
            }

            // Healing is a success, but a step that keeps needing rescue is running on a locator that
            // no longer matches the application. Reported separately: it can coexist with the above.
            if (heals >= MinHealsForDrift)
            {
                var lastHeal = ordered.Last(r => r.Status == StepRunStatus.Healed);
                insights.Add(new AiInsightDto
                {
                    Id = $"drift:{scenario.Id}:{group.Key.StepOrder}",
                    Category = "Maintenance",
                    Severity = "Warning",
                    Title = $"{stepLabel} keeps needing self-healing — {scenario.Title}",
                    Summary = $"\"{Truncate(action, 90)}\" was auto-healed in {heals} of {runs} runs. "
                              + "Healing masks locator drift; each rescue costs run time and may eventually pick the wrong element.",
                    SuggestedAction = "Re-record this step so its primary locator matches the current application instead of relying on healing every run.",
                    Evidence = BuildTimeline(ordered),
                    ProjectId = scenario.ProjectId,
                    ProjectName = scenario.ProjectName,
                    ScenarioId = scenario.Id,
                    ScenarioTitle = scenario.Title,
                    LastSeenUtc = lastHeal.CreatedAtUtc,
                });
            }
        }
    }

    /// <summary>Scenarios that exist but prove nothing, because they never ran or stopped running.</summary>
    private static void AddCoverageInsights(
        List<AiInsightDto> insights,
        IReadOnlyList<ResultRow> results,
        IReadOnlyList<StepRow> steps,
        IReadOnlyDictionary<Guid, ScenarioMeta> scenarios,
        DateTimeOffset now)
    {
        var lastRunByScenario = results
            .GroupBy(r => r.ScenarioId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.CreatedAtUtc));

        var stepCounts = steps
            .GroupBy(s => s.ScenarioId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var byProject in scenarios.Values.GroupBy(s => s.ProjectId))
        {
            // Only authored scenarios count — an empty scenario is a different problem.
            var neverRun = byProject
                .Where(s => stepCounts.GetValueOrDefault(s.Id) > 0 && !lastRunByScenario.ContainsKey(s.Id))
                .ToList();

            if (neverRun.Count > 0)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"never-run:{byProject.Key}",
                    Category = "Coverage",
                    Severity = "Warning",
                    Title = $"{neverRun.Count} scenario(s) have never been executed",
                    Summary = $"Authored but with no run history, so they are proving nothing: {NameList(neverRun.Select(s => s.Title))}.",
                    SuggestedAction = "Run them once to establish a baseline — an unexecuted scenario cannot catch a regression.",
                    Evidence = neverRun.Take(MaxEvidenceLines)
                        .Select(s => $"{s.Title} — {stepCounts.GetValueOrDefault(s.Id)} step(s), never run")
                        .ToList(),
                    ProjectId = byProject.Key,
                    ProjectName = byProject.First().ProjectName,
                });
            }

            var stale = byProject
                .Where(s => lastRunByScenario.TryGetValue(s.Id, out var at) && (now - at).TotalDays > StaleAfterDays)
                .Select(s => new { Scenario = s, LastRun = lastRunByScenario[s.Id] })
                .OrderBy(x => x.LastRun)
                .ToList();

            if (stale.Count > 0)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"stale:{byProject.Key}",
                    Category = "Coverage",
                    Severity = "Info",
                    Title = $"{stale.Count} scenario(s) have not run in over {StaleAfterDays} days",
                    Summary = $"The newest result for these scenarios is more than {StaleAfterDays} days old, so their last known verdict may no longer reflect the application.",
                    SuggestedAction = "Add them to a scheduled suite so coverage stays current instead of decaying silently.",
                    Evidence = stale.Take(MaxEvidenceLines)
                        .Select(x => $"{x.Scenario.Title} — last run {(int)(now - x.LastRun).TotalDays} days ago")
                        .ToList(),
                    ProjectId = byProject.Key,
                    ProjectName = byProject.First().ProjectName,
                    LastSeenUtc = stale[^1].LastRun,
                });
            }
        }
    }

    /// <summary>Authoring problems that make a scenario unreliable before it is ever run.</summary>
    private static void AddAuthoringInsights(
        List<AiInsightDto> insights,
        IReadOnlyList<StepRow> steps,
        IReadOnlyDictionary<Guid, ScenarioMeta> scenarios)
    {
        foreach (var byProject in steps.GroupBy(s => scenarios[s.ScenarioId].ProjectId))
        {
            var projectName = scenarios[byProject.First().ScenarioId].ProjectName;

            var needsReview = byProject.Where(s => s.NeedsReview).ToList();
            if (needsReview.Count > 0)
            {
                var affected = needsReview.Select(s => scenarios[s.ScenarioId].Title).Distinct().Count();
                insights.Add(new AiInsightDto
                {
                    Id = $"needs-review:{byProject.Key}",
                    Category = "Quality",
                    Severity = "Warning",
                    Title = $"{needsReview.Count} step(s) are flagged for review",
                    Summary = $"The agent could not confidently record or interpret these steps across {affected} scenario(s). "
                              + "Unreviewed steps are the most common source of false failures.",
                    SuggestedAction = "Open each flagged step and either correct the action or re-record it.",
                    Evidence = needsReview.Take(MaxEvidenceLines)
                        .Select(s => $"{scenarios[s.ScenarioId].Title} · step {s.Order}: "
                                     + (string.IsNullOrWhiteSpace(s.ReviewReason)
                                         ? Truncate(s.Action, 80)
                                         : Truncate(s.ReviewReason!, 110)))
                        .ToList(),
                    ProjectId = byProject.Key,
                    ProjectName = projectName,
                });
            }

            // Without recorded actions a run has to re-derive the step at execution time, which is
            // slower and can reinterpret the step differently on each run.
            var unrecorded = byProject
                .Where(s => string.IsNullOrWhiteSpace(s.RecordedActionsJson) || s.RecordedActionsJson!.Trim() == "[]")
                .ToList();

            if (unrecorded.Count > 0)
            {
                var affected = unrecorded.Select(s => scenarios[s.ScenarioId].Title).Distinct().Count();
                insights.Add(new AiInsightDto
                {
                    Id = $"unrecorded:{byProject.Key}",
                    Category = "Quality",
                    Severity = "Info",
                    Title = $"{unrecorded.Count} step(s) have no recorded actions",
                    Summary = $"Across {affected} scenario(s), these steps have never been captured by an exploration run, "
                              + "so replay must re-interpret them each time instead of executing a deterministic script.",
                    SuggestedAction = "Explore the affected scenarios once to record concrete actions, which makes later runs faster and repeatable.",
                    Evidence = unrecorded.Take(MaxEvidenceLines)
                        .Select(s => $"{scenarios[s.ScenarioId].Title} · step {s.Order}: {Truncate(s.Action, 80)}")
                        .ToList(),
                    ProjectId = byProject.Key,
                    ProjectName = projectName,
                });
            }
        }
    }

    /// <summary>Features extracted from requirements that no scenario references.</summary>
    private static void AddFeatureCoverageInsights(
        List<AiInsightDto> insights,
        IReadOnlyList<FeatureRow> features,
        IEnumerable<Guid> coveredFeatureIds,
        IReadOnlyDictionary<Guid, string> projectNames,
        Guid? filter)
    {
        var covered = coveredFeatureIds.ToHashSet();

        var uncovered = features
            .Where(f => !covered.Contains(f.Id))
            .Where(f => filter is null || f.ProjectId == filter)
            .GroupBy(f => f.ProjectId);

        foreach (var byProject in uncovered)
        {
            var names = byProject.Select(f => f.Name).ToList();
            insights.Add(new AiInsightDto
            {
                Id = $"uncovered-features:{byProject.Key}",
                Category = "Coverage",
                Severity = "Warning",
                Title = $"{names.Count} feature(s) have no scenarios",
                Summary = $"Extracted from requirements but referenced by no test scenario: {NameList(names)}.",
                SuggestedAction = "Generate scenarios for these features so the requirements they came from are actually verified.",
                Evidence = names.Take(MaxEvidenceLines).Select(n => $"{n} — 0 scenarios").ToList(),
                ProjectId = byProject.Key,
                ProjectName = projectNames.TryGetValue(byProject.Key, out var name) ? name : "Unknown project",
            });
        }
    }

    /// <summary>Requirements stuck before analysis never become features, so nothing downstream exists.</summary>
    private async Task AddRequirementInsightsAsync(
        List<AiInsightDto> insights,
        IReadOnlyDictionary<Guid, string> projectNames,
        Guid? filter,
        CancellationToken cancellationToken)
    {
        var requirements = await _db.Requirements
            .AsNoTracking()
            .Where(r => filter == null || r.ProjectId == filter)
            .Select(r => new { r.ProjectId, r.Name, r.Status, r.ErrorMessage, r.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        foreach (var byProject in requirements.GroupBy(r => r.ProjectId))
        {
            var projectName = projectNames.TryGetValue(byProject.Key, out var name) ? name : "Unknown project";

            var failed = byProject.Where(r => r.Status == RequirementStatus.Failed).ToList();
            if (failed.Count > 0)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"requirement-failed:{byProject.Key}",
                    Category = "Coverage",
                    Severity = "Critical",
                    Title = $"{failed.Count} requirement(s) failed analysis",
                    Summary = "Analysis never completed for these documents, so no modules, features or scenarios were derived from them. "
                              + "Everything they specify is currently untested.",
                    SuggestedAction = "Re-run analysis. If extraction keeps failing, check the document is text-based rather than scanned images.",
                    Evidence = failed.Take(MaxEvidenceLines)
                        .Select(r => $"{r.Name} — {Truncate(r.ErrorMessage ?? "no error recorded", 120)}")
                        .ToList(),
                    ProjectId = byProject.Key,
                    ProjectName = projectName,
                    LastSeenUtc = failed.Max(r => r.CreatedAtUtc),
                });
            }

            var pending = byProject.Where(r => r.Status == RequirementStatus.Uploaded).ToList();
            if (pending.Count > 0)
            {
                insights.Add(new AiInsightDto
                {
                    Id = $"requirement-pending:{byProject.Key}",
                    Category = "Coverage",
                    Severity = "Info",
                    Title = $"{pending.Count} requirement(s) are awaiting analysis",
                    Summary = $"Uploaded but never analyzed: {NameList(pending.Select(r => r.Name))}. No coverage exists for them yet.",
                    SuggestedAction = "Run analysis to extract modules and features, then generate scenarios from them.",
                    Evidence = pending.Take(MaxEvidenceLines).Select(r => $"{r.Name} — uploaded, not analyzed").ToList(),
                    ProjectId = byProject.Key,
                    ProjectName = projectName,
                });
            }
        }
    }

    /// <summary>Runs that died before producing verdicts — an infrastructure signal, not a test signal.</summary>
    private async Task AddRunFailureInsightsAsync(
        List<AiInsightDto> insights,
        IReadOnlyDictionary<Guid, string> projectNames,
        Guid? filter,
        CancellationToken cancellationToken)
    {
        var failedSessions = await _db.ExplorationSessions
            .AsNoTracking()
            .Where(s => s.Status == ExplorationStatus.Failed)
            .Where(s => filter == null || s.ProjectId == filter)
            .Select(s => new { s.ProjectId, s.ErrorMessage, s.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        foreach (var byProject in failedSessions.GroupBy(s => s.ProjectId))
        {
            var withMessage = byProject.Where(s => !string.IsNullOrWhiteSpace(s.ErrorMessage)).ToList();
            if (withMessage.Count < 2) continue;

            // Cluster on the leading fragment: the tail usually carries run-specific ids and URLs.
            var top = withMessage
                .GroupBy(s => Truncate(s.ErrorMessage!, 60))
                .OrderByDescending(g => g.Count())
                .First();

            if (top.Count() < 2) continue;

            insights.Add(new AiInsightDto
            {
                Id = $"run-failures:{byProject.Key}",
                Category = "Reliability",
                Severity = "Critical",
                Title = $"{top.Count()} runs aborted with the same error",
                Summary = $"Multiple executions ended before producing verdicts, all reporting: \"{top.Key}\". "
                          + "A repeated abort is usually environment or agent configuration, not the application under test.",
                SuggestedAction = "Check the environment's base URL, credentials and browser availability before trusting recent pass rates.",
                Evidence = top.OrderByDescending(s => s.CreatedAtUtc).Take(MaxEvidenceLines)
                    .Select(s => $"{s.CreatedAtUtc:yyyy-MM-dd HH:mm} — {Truncate(s.ErrorMessage!, 110)}")
                    .ToList(),
                ProjectId = byProject.Key,
                ProjectName = projectNames.TryGetValue(byProject.Key, out var name) ? name : "Unknown project",
                LastSeenUtc = top.Max(s => s.CreatedAtUtc),
            });
        }
    }

    /// <summary>
    /// Evidence for a step verdict: a compact outcome sequence followed by the most recent runs in
    /// detail. The sequence matters because the newest few runs often all agree — showing only those
    /// would fail to demonstrate a flakiness claim made over a longer window.
    /// </summary>
    private static IReadOnlyList<string> BuildTimeline(IReadOnlyList<ResultRow> ordered)
    {
        var recent = ordered.OrderByDescending(r => r.CreatedAtUtc).ToList();

        var sequence = string.Join(" · ", recent.Take(SequenceLength).Select(r => r.Status.ToString()));
        var lines = new List<string>
        {
            $"Outcomes (newest first): {sequence}"
                + (recent.Count > SequenceLength ? $" · +{recent.Count - SequenceLength} older" : string.Empty),
        };

        lines.AddRange(recent
            .Take(MaxEvidenceLines)
            .Select(r => $"{r.CreatedAtUtc:yyyy-MM-dd HH:mm} — {r.Status}"
                         + (string.IsNullOrWhiteSpace(r.Detail) ? string.Empty : $": {Truncate(r.Detail!, 110)}")));

        return lines;
    }

    private static string NameList(IEnumerable<string> names)
    {
        var list = names.ToList();
        var shown = string.Join(", ", list.Take(MaxNamesInSummary).Select(n => $"\"{Truncate(n, 60)}\""));
        return list.Count > MaxNamesInSummary ? $"{shown} and {list.Count - MaxNamesInSummary} more" : shown;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "Warning" => 1,
        _ => 2,
    };

    private sealed record ScenarioMeta(Guid Id, string Title, Guid ProjectId, string ProjectName);

    private sealed record FeatureRow(Guid Id, string Name, Guid ProjectId);

    private sealed record StepRow(
        Guid ScenarioId,
        int Order,
        string Action,
        bool NeedsReview,
        string? ReviewReason,
        string? RecordedActionsJson);

    private sealed record ResultRow(
        Guid SessionId,
        Guid ScenarioId,
        int StepOrder,
        string Action,
        StepRunStatus Status,
        string? Detail,
        DateTimeOffset CreatedAtUtc);
}
