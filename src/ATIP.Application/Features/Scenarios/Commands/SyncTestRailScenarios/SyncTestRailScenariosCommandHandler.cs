using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.SyncTestRailScenarios;

public sealed class SyncTestRailScenariosCommandHandler
    : IRequestHandler<SyncTestRailScenariosCommand, IReadOnlyList<ScenarioDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITestRailClient _testRail;

    public SyncTestRailScenariosCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        ITestRailClient testRail)
    {
        _db = db;
        _currentUser = currentUser;
        _testRail = testRail;
    }

    public async Task<IReadOnlyList<ScenarioDto>> Handle(
        SyncTestRailScenariosCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        var feature = await _db.Features
            .Include(f => f.Scenarios)
            .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(f => f.Id == request.FeatureId && f.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Feature), request.FeatureId);

        var imported = await _testRail.GetCasesAsync(
            request.TestRailProjectId,
            request.SuiteId,
            request.SectionId,
            cancellationToken);

        if (imported.Count == 0)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(nameof(request.TestRailProjectId), "No test cases were returned from TestRail for the selected filters."),
            ]);
        }

        var existing = feature.Scenarios.Where(s => s.Source == ScenarioSource.TestRail).ToList();
        _db.Scenarios.RemoveRange(existing);

        var created = new List<Scenario>(imported.Count);
        foreach (var testCase in imported)
        {
            var tags = new List<string> { "import:testrail" };
            if (!string.IsNullOrWhiteSpace(testCase.ExternalReference))
            {
                tags.Add($"testrail:{testCase.ExternalReference}");
            }

            var scenario = new Scenario
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                FeatureId = request.FeatureId,
                Title = Truncate(testCase.Title, 300),
                Type = InferType(testCase.Title),
                Priority = Priority.Medium,
                Risk = RiskLevel.Medium,
                Source = ScenarioSource.TestRail,
                Preconditions = testCase.Preconditions,
                ExpectedResult = testCase.ExpectedResult,
                TagsJson = JsonSerializer.Serialize(tags),
            };

            foreach (var step in testCase.Steps.OrderBy(s => s.Order))
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

        await _db.SaveChangesAsync(cancellationToken);

        return created
            .OrderBy(s => s.Title)
            .Select(ScenarioDto.FromEntity)
            .ToList();
    }

    private static ScenarioType InferType(string title)
    {
        var t = title.ToLowerInvariant();
        if (t.Contains("negative") || t.Contains("invalid") || t.Contains("error")) return ScenarioType.Negative;
        if (t.Contains("security") || t.Contains("auth") || t.Contains("rbac")) return ScenarioType.Security;
        if (t.Contains("smoke")) return ScenarioType.Smoke;
        if (t.Contains("boundary") || t.Contains("limit") || t.Contains("edge")) return ScenarioType.Boundary;
        return ScenarioType.Positive;
    }

    private static string Truncate(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
