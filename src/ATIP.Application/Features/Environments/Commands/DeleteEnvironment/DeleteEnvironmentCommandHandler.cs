using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Application.Features.Environments.Commands.DeleteEnvironment;

public sealed class DeleteEnvironmentCommandHandler : IRequestHandler<DeleteEnvironmentCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteEnvironmentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var environment = await _db.Environments
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EnvEntity), request.Id);

        _db.Environments.Remove(environment);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
