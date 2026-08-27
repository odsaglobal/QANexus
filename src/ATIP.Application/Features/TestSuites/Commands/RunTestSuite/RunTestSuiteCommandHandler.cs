using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
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
            SeedUrl = environment.BaseUrl,
            MaxPages = 50,
            MaxDepth = 3,
        };

        _db.ExplorationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(session.Id, cancellationToken);

        await _audit.LogAsync("suite.run", "Suite", $"Ran suite \"{suite.Name}\" ({suite.Scenarios.Count} scenarios)", nameof(TestSuite), suite.Id, cancellationToken);

        return ExplorationSessionDto.FromEntity(session);
    }
}
