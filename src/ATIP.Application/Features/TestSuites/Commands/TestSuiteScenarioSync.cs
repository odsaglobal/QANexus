using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Commands;

/// <summary>Shared logic for validating and (re)building a suite's ordered scenario membership.</summary>
public static class TestSuiteScenarioSync
{
    /// <summary>
    /// Replaces a suite's scenario membership. Operates directly on the <see cref="TestSuiteScenario"/>
    /// DbSet (bulk delete + insert) rather than the <c>suite.Scenarios</c> navigation collection, because
    /// when <paramref name="suite"/> is an ALREADY-TRACKED (Unchanged) entity (e.g. loaded for an update),
    /// mutating a tracked navigation collection makes EF's is-key-set heuristic treat the new
    /// client-generated-key rows as EXISTING, producing an UPDATE that affects 0 rows
    /// (DbUpdateConcurrencyException) instead of an INSERT. Bulk delete + direct DbSet.Add avoids that
    /// entirely and works the same whether the suite is new or pre-existing.
    /// </summary>
    public static async Task ReplaceScenariosAsync(
        IApplicationDbContext db,
        TestSuite suite,
        IReadOnlyList<Guid> scenarioIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // De-duplicate while preserving order.
        var orderedIds = scenarioIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (orderedIds.Count > 0)
        {
            var validIds = await db.Scenarios
                .Where(s => orderedIds.Contains(s.Id) && s.ProjectId == suite.ProjectId)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var missing = orderedIds.Where(id => !validIds.Contains(id)).ToList();
            if (missing.Count > 0)
            {
                throw new ValidationException(
                [
                    new FluentValidation.Results.ValidationFailure(
                        "ScenarioIds",
                        $"One or more scenarios do not belong to this project: {string.Join(", ", missing)}."),
                ]);
            }
        }

        await db.TestSuiteScenarios
            .IgnoreQueryFilters()
            .Where(ss => ss.SuiteId == suite.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var order = 1;
        foreach (var id in orderedIds)
        {
            db.TestSuiteScenarios.Add(new TestSuiteScenario
            {
                TenantId = tenantId,
                SuiteId = suite.Id,
                ScenarioId = id,
                Order = order++,
            });
        }
    }
}
