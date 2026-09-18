using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Scenarios.Common;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Application.Features.Scenarios.Import;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

public sealed class ImportScenariosFromFileCommandHandler
    : IRequestHandler<ImportScenariosFromFileCommand, ImportScenariosResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ImportScenariosFromFileCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ImportScenariosResult> Handle(
        ImportScenariosFromFileCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        Feature feature;
        if (request.FeatureId == Guid.Empty)
        {
            feature = await ScenarioBucket.EnsureAsync(_db, tenantId, request.ProjectId, cancellationToken);
        }
        else
        {
            feature = await _db.Features
                .FirstOrDefaultAsync(f => f.Id == request.FeatureId && f.ProjectId == request.ProjectId, cancellationToken)
                ?? throw new NotFoundException(nameof(Feature), request.FeatureId);
        }

        var parsed = ScenarioImportParser.Parse(request.FileName, request.Content);
        if (parsed.Count == 0)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(nameof(request.Content), "No valid scenarios with steps were found in the uploaded file."),
            ]);
        }

        // Import is purely additive. It previously deleted the feature's existing Manual
        // scenarios first, but only ever on the explicit-feature path: the default bucket path
        // loads the feature without its Scenarios collection, so that delete silently matched
        // nothing. Deleting is also the wrong default now that imports can join a suite — a
        // re-import would soft-delete scenarios that suites still reference.
        var created = new List<Scenario>(parsed.Count);
        foreach (var imported in parsed)
        {
            var tags = new List<string> { "import:manual" };
            if (!string.IsNullOrWhiteSpace(imported.ExternalReference))
            {
                tags.Add($"external:{imported.ExternalReference}");
            }

            var scenario = new Scenario
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                FeatureId = feature.Id,
                Title = Truncate(imported.Title, 300),
                Type = ScenarioType.Positive,
                Priority = Priority.Medium,
                Risk = RiskLevel.Medium,
                Source = ScenarioSource.Manual,
                Preconditions = imported.Preconditions,
                ExpectedResult = imported.ExpectedResult,
                TagsJson = JsonSerializer.Serialize(tags),
            };

            foreach (var step in imported.Steps.OrderBy(s => s.Order))
            {
                scenario.Steps.Add(new ScenarioStep
                {
                    TenantId = tenantId,
                    ScenarioId = scenario.Id,
                    Order = step.Order,
                    Action = Truncate(step.Action, 1000),
                    ExpectedResult = step.ExpectedResult,
                });
            }

            _db.Scenarios.Add(scenario);
            created.Add(scenario);
        }

        var suite = await ResolveSuiteAsync(request, tenantId, cancellationToken);
        if (suite is not null)
        {
            // Append after whatever the suite already holds, so an import into an existing suite
            // never reorders or drops its current scenarios. Rows are added straight to the DbSet
            // rather than through suite.Scenarios: mutating a tracked navigation collection makes
            // EF treat client-generated keys as existing rows and emit a no-op UPDATE.
            var existing = await _db.TestSuiteScenarios
                .IgnoreQueryFilters()
                .Where(ss => ss.SuiteId == suite.Id)
                .Select(ss => new { ss.ScenarioId, ss.Order })
                .ToListAsync(cancellationToken);

            var existingScenarioIds = existing.Select(e => e.ScenarioId).ToHashSet();
            var nextOrder = existing.Count == 0 ? 1 : existing.Max(e => e.Order) + 1;

            foreach (var scenario in created.Where(s => !existingScenarioIds.Contains(s.Id)))
            {
                _db.TestSuiteScenarios.Add(new TestSuiteScenario
                {
                    TenantId = tenantId,
                    SuiteId = suite.Id,
                    ScenarioId = scenario.Id,
                    Order = nextOrder++,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ImportScenariosResult
        {
            Scenarios = created
                .OrderBy(s => s.Title)
                .Select(ScenarioDto.FromEntity)
                .ToList(),
            SuiteId = suite?.Id,
            SuiteName = suite?.Name,
            SuiteCreated = suite is not null && request.SuiteMode == ImportSuiteMode.New,
        };
    }

    /// <summary>
    /// Resolves the suite the imported scenarios should join. Returns null for
    /// <see cref="ImportSuiteMode.None"/>. A new suite is added to the change tracker but not
    /// saved here, so the whole import commits as one transaction.
    /// </summary>
    private async Task<TestSuite?> ResolveSuiteAsync(
        ImportScenariosFromFileCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        switch (request.SuiteMode)
        {
            case ImportSuiteMode.Existing:
                var suiteId = request.SuiteId ?? Guid.Empty;
                return await _db.TestSuites
                    .FirstOrDefaultAsync(
                        s => s.Id == suiteId && s.ProjectId == request.ProjectId,
                        cancellationToken)
                    ?? throw new NotFoundException(nameof(TestSuite), suiteId);

            case ImportSuiteMode.New:
                var name = (request.NewSuiteName ?? string.Empty).Trim();
                var duplicate = await _db.TestSuites.AnyAsync(
                    s => s.ProjectId == request.ProjectId && s.Name == name,
                    cancellationToken);
                if (duplicate)
                {
                    throw new ValidationException([
                        new FluentValidation.Results.ValidationFailure(
                            nameof(request.NewSuiteName),
                            $"A suite named \"{name}\" already exists in this project."),
                    ]);
                }

                var created = new TestSuite
                {
                    TenantId = tenantId,
                    ProjectId = request.ProjectId,
                    Name = Truncate(name, 200),
                    Description = $"Created from import of {request.FileName}.",
                };
                _db.TestSuites.Add(created);
                return created;

            default:
                return null;
        }
    }

    private static string Truncate(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
