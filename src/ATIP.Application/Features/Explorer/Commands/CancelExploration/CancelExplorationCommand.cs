using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Commands.CancelExploration;

public sealed record CancelExplorationCommand(Guid Id) : IRequest;

public sealed class CancelExplorationCommandHandler : IRequestHandler<CancelExplorationCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IExplorationCancellationRegistry _cancellationRegistry;

    public CancelExplorationCommandHandler(IApplicationDbContext db, IExplorationCancellationRegistry cancellationRegistry)
    {
        _db = db;
        _cancellationRegistry = cancellationRegistry;
    }

    public async Task Handle(CancelExplorationCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.ExplorationSessions
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ExplorationSession), request.Id);

        if (session.Status is ExplorationStatus.Completed or ExplorationStatus.Failed)
        {
            return; // No-op for terminal states.
        }

        session.Status = ExplorationStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);

        // Signals the actual in-flight agent loop to stop (if it's currently running on this instance).
        // Without this, cancellation only flipped the DB flag and the browser/LLM loop ran to completion
        // regardless, silently overwriting this status once it finished.
        _cancellationRegistry.RequestCancellation(request.Id);
    }
}
