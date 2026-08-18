using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestSuites.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Queries.ListTestSuites;

public sealed class ListTestSuitesQueryHandler
    : IRequestHandler<ListTestSuitesQuery, IReadOnlyList<TestSuiteDto>>
{
    private readonly IApplicationDbContext _db;

    public ListTestSuitesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TestSuiteDto>> Handle(
        ListTestSuitesQuery request,
        CancellationToken cancellationToken)
    {
        var suites = await _db.TestSuites
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId)
            .Include(s => s.Scenarios).ThenInclude(ss => ss.Scenario)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return suites.Select(TestSuiteDto.FromEntity).ToList();
    }
}
