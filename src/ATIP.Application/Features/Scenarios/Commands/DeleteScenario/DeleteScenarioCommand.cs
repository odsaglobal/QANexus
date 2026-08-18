using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.DeleteScenario;

public sealed record DeleteScenarioCommand(Guid Id) : IRequest;

public sealed class DeleteScenarioCommandHandler : IRequestHandler<DeleteScenarioCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteScenarioCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteScenarioCommand request, CancellationToken cancellationToken)
    {
        var scenario = await _db.Scenarios
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Scenario), request.Id);

        scenario.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
