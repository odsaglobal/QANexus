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

    public CancelExplorationCommandHandler(IApplicationDbContext db) => _db = db;

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
    }
}
