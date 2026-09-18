using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.DataConnections.Commands.DeleteDataConnection;

public sealed class DeleteDataConnectionCommandHandler : IRequestHandler<DeleteDataConnectionCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteDataConnectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteDataConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _db.DataConnections
            .FirstOrDefaultAsync(
                c => c.Id == request.Id && c.EnvironmentId == request.EnvironmentId && !c.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(DataConnection), request.Id);

        // Soft delete: a scenario may still reference this connection by name, and a hard delete
        // would turn a recoverable configuration mistake into an unexplained run failure.
        connection.IsDeleted = true;
        connection.DeletedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
