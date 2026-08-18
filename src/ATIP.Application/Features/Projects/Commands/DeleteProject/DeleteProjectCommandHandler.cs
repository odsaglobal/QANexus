using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Commands.DeleteProject;

public sealed class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteProjectCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.Id);

        // Soft delete is applied by the SaveChanges interceptor when IsDeleted flips to true.
        project.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
