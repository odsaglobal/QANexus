using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Commands.StartExploration;

public sealed class StartExplorationCommandHandler : IRequestHandler<StartExplorationCommand, ExplorationSessionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExplorationQueue _queue;

    public StartExplorationCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IExplorationQueue queue)
    {
        _db = db;
        _currentUser = currentUser;
        _queue = queue;
    }

    public async Task<ExplorationSessionDto> Handle(StartExplorationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var environment = await _db.Environments
            .FirstOrDefaultAsync(e => e.Id == request.EnvironmentId && e.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Environment), request.EnvironmentId);

        var session = new ExplorationSession
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            EnvironmentId = environment.Id,
            FeatureId = request.FeatureId,
            ScenarioId = request.ScenarioId,
            Prompt = string.IsNullOrWhiteSpace(request.Prompt) ? null : request.Prompt.Trim(),
            SeedUrl = string.IsNullOrWhiteSpace(request.SeedUrl) ? environment.BaseUrl : request.SeedUrl.Trim(),
            MaxPages = request.MaxPages,
            MaxDepth = request.MaxDepth,
        };

        _db.ExplorationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        // Queue for background processing — returns immediately with Pending status.
        await _queue.EnqueueAsync(session.Id, cancellationToken);

        return ExplorationSessionDto.FromEntity(session);
    }
}
