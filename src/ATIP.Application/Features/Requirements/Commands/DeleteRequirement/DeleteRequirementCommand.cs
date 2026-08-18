using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Requirements.Commands.DeleteRequirement;

public sealed record DeleteRequirementCommand(Guid Id) : IRequest;

public sealed class DeleteRequirementCommandHandler : IRequestHandler<DeleteRequirementCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteRequirementCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteRequirementCommand request, CancellationToken cancellationToken)
    {
        var requirement = await _db.Requirements
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Requirement), request.Id);

        requirement.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
