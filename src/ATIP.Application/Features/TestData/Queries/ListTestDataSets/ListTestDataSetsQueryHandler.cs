using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestData.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestData.Queries.ListTestDataSets;

public sealed class ListTestDataSetsQueryHandler
    : IRequestHandler<ListTestDataSetsQuery, IReadOnlyList<TestDataSetDto>>
{
    private readonly IApplicationDbContext _db;

    public ListTestDataSetsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TestDataSetDto>> Handle(
        ListTestDataSetsQuery request,
        CancellationToken cancellationToken)
    {
        var dataSets = await _db.TestDataSets
            .AsNoTracking()
            .Where(d => d.ProjectId == request.ProjectId && d.EnvironmentId == request.EnvironmentId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        return dataSets.Select(TestDataSetDto.FromEntity).ToList();
    }
}
