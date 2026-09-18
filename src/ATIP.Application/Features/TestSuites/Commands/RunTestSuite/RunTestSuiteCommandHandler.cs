using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Commands.RunTestSuite;

public sealed class RunTestSuiteCommandHandler : IRequestHandler<RunTestSuiteCommand, ExplorationSessionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExplorationQueue _queue;
    private readonly IAuditLogger _audit;

    public RunTestSuiteCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IExplorationQueue queue,
        IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _queue = queue;
        _audit = audit;
    }

    public async Task<ExplorationSessionDto> Handle(RunTestSuiteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        var suite = await _db.TestSuites
            .Include(s => s.Scenarios)
            .FirstOrDefaultAsync(s => s.Id == request.SuiteId && s.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(TestSuite), request.SuiteId);

        if (suite.Scenarios.Count == 0)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "SuiteId", "This suite has no scenarios to run. Add scenarios first."),
            ]);
        }

        // Resolve the filter now so an impossible one fails fast with a clear message, rather than
        // queueing a run that quietly executes nothing.
        var filter = ScenarioRunFilter.Create(request.Tags, request.Types, request.Priorities, request.ScenarioIds);
        var matchCount = suite.Scenarios.Count;
        if (!filter.IsEmpty)
        {
            var scenarioIds = suite.Scenarios.Select(s => s.ScenarioId).ToList();
            var facets = await _db.Scenarios
                .Where(s => scenarioIds.Contains(s.Id))
                .Select(s => new { s.Id, s.TagsJson, s.Type, s.Priority })
                .ToListAsync(cancellationToken);

            matchCount = facets.Count(f => filter.Matches(f.Id, f.TagsJson, f.Type, f.Priority));
            if (matchCount == 0)
            {
                throw new ValidationException(
                [
                    new FluentValidation.Results.ValidationFailure(
                        "Tags", $"No scenario in this suite matches {filter.Describe()}."),
                ]);
            }
        }

        Domain.Entities.Environment? environment;
        if (request.EnvironmentId is { } envId)
        {
            environment = await _db.Environments
                .FirstOrDefaultAsync(e => e.Id == envId && e.ProjectId == request.ProjectId, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.Environment), envId);
        }
        else
        {
            var environments = await _db.Environments
                .Where(e => e.ProjectId == request.ProjectId)
                .ToListAsync(cancellationToken);

            environment = environments.FirstOrDefault(e => e.IsDefault) ?? environments.FirstOrDefault();
            if (environment is null)
            {
                throw new ValidationException(
                [
                    new FluentValidation.Results.ValidationFailure(
                        "EnvironmentId", "Add an environment with a base URL before running a suite."),
                ]);
            }
        }

        var session = new ExplorationSession
        {
            TenantId = tenantId,
            ProjectId = request.ProjectId,
            EnvironmentId = environment.Id,
            SuiteId = suite.Id,
            RunFilterJson = filter.Serialize(),
            SeedUrl = environment.BaseUrl,
            MaxPages = 50,
            MaxDepth = 3,
        };

        _db.ExplorationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(session.Id, cancellationToken);

        var scope = filter.IsEmpty
            ? $"{suite.Scenarios.Count} scenarios"
            : $"{matchCount} of {suite.Scenarios.Count} scenarios — {filter.Describe()}";
        await _audit.LogAsync("suite.run", "Suite", $"Ran suite \"{suite.Name}\" ({scope})", nameof(TestSuite), suite.Id, cancellationToken);

        return ExplorationSessionDto.FromEntity(session);
    }
}
