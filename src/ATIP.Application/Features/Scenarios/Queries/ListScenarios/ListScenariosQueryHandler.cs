using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Queries.ListScenarios;

public sealed class ListScenariosQueryHandler : IRequestHandler<ListScenariosQuery, IReadOnlyList<ScenarioDto>>
{
    private readonly IApplicationDbContext _db;

    public ListScenariosQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScenarioDto>> Handle(
        ListScenariosQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Scenarios
            .AsNoTracking()
            .Include(s => s.Steps)
            .Where(s => s.ProjectId == request.ProjectId);

        if (request.FeatureId is { } featureId)
        {
            query = query.Where(s => s.FeatureId == featureId);
        }

        var scenarios = await query
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Title)
            .ToListAsync(cancellationToken);

        return scenarios.Select(ScenarioDto.FromEntity).ToList();
    }
}
