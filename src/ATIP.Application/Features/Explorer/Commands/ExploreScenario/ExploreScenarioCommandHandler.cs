using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Commands.ExploreScenario;

public sealed class ExploreScenarioCommandHandler : IRequestHandler<ExploreScenarioCommand, ExplorationSessionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExplorationQueue _queue;

    public ExploreScenarioCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IExplorationQueue queue)
    {
        _db = db;
        _currentUser = currentUser;
        _queue = queue;
    }

    public async Task<ExplorationSessionDto> Handle(ExploreScenarioCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        var scenario = await _db.Scenarios
            .FirstOrDefaultAsync(s => s.Id == request.ScenarioId && s.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Scenario), request.ScenarioId);

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
                throw new ValidationException([
                    new FluentValidation.Results.ValidationFailure("EnvironmentId", "Add an environment with a base URL before exploring."),
                ]);
            }
        }

        var session = new ExplorationSession
        {
            TenantId = tenantId,
            ProjectId = request.ProjectId,
            EnvironmentId = environment.Id,
            FeatureId = scenario.FeatureId,
            ScenarioId = scenario.Id,
            ExecuteSavedSteps = request.ExecuteSavedSteps,
            SeedUrl = environment.BaseUrl,
            MaxPages = 25,
            MaxDepth = 3,
        };

        _db.ExplorationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(session.Id, cancellationToken);

        return ExplorationSessionDto.FromEntity(session);
    }
}
