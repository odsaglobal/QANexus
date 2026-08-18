using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Commands;

/// <summary>Shared logic for validating and (re)building a suite's ordered scenario membership.</summary>
public static class TestSuiteScenarioSync
{
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

        if (orderedIds.Count == 0)
        {
            suite.Scenarios.Clear();
            return;
        }

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

        suite.Scenarios.Clear();
        var order = 1;
        foreach (var id in orderedIds)
        {
            suite.Scenarios.Add(new TestSuiteScenario
            {
                TenantId = tenantId,
                SuiteId = suite.Id,
                ScenarioId = id,
                Order = order++,
            });
        }
    }
}
