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

        // Suite membership is a join row, not a soft-deletable entity, so it survives the scenario it
        // points at. Left behind it inflates every suite's scenarioCount and feeds phantom scenarios
        // to suite runs.
        var memberships = await _db.TestSuiteScenarios
            .Where(m => m.ScenarioId == scenario.Id)
            .ToListAsync(cancellationToken);
        _db.TestSuiteScenarios.RemoveRange(memberships);

        scenario.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
