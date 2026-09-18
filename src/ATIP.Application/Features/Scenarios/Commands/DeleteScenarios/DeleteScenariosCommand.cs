using ATIP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.DeleteScenarios;

/// <summary>Soft-deletes several test cases at once. Ids that do not belong to the project are ignored.</summary>
public sealed record DeleteScenariosCommand(Guid ProjectId, IReadOnlyList<Guid> Ids) : IRequest<DeleteScenariosResult>;

public sealed record DeleteScenariosResult(int Deleted);

public sealed class DeleteScenariosCommandHandler : IRequestHandler<DeleteScenariosCommand, DeleteScenariosResult>
{
    private readonly IApplicationDbContext _db;

    public DeleteScenariosCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<DeleteScenariosResult> Handle(DeleteScenariosCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();

        // Scoped to the project on purpose: the caller supplies the ids, so without this a valid id
        // from a sibling project in the same tenant would delete just as happily.
        var scenarios = await _db.Scenarios
            .Where(s => s.ProjectId == request.ProjectId && ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (scenarios.Count == 0)
        {
            return new DeleteScenariosResult(0);
        }

        var deletedIds = scenarios.Select(s => s.Id).ToList();

        // Suite membership is a join row, not a soft-deletable entity, so it survives the scenario it
        // points at. Left behind it inflates every suite's scenarioCount and feeds phantom scenarios
        // to suite runs.
        var memberships = await _db.TestSuiteScenarios
            .Where(m => deletedIds.Contains(m.ScenarioId))
            .ToListAsync(cancellationToken);
        _db.TestSuiteScenarios.RemoveRange(memberships);

        foreach (var scenario in scenarios)
        {
            scenario.IsDeleted = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new DeleteScenariosResult(scenarios.Count);
    }
}
