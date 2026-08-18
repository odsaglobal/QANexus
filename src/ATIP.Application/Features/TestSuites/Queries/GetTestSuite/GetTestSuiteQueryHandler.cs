using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestSuites.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Queries.GetTestSuite;

public sealed class GetTestSuiteQueryHandler : IRequestHandler<GetTestSuiteQuery, TestSuiteDto>
{
    private readonly IApplicationDbContext _db;

    public GetTestSuiteQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TestSuiteDto> Handle(GetTestSuiteQuery request, CancellationToken cancellationToken)
    {
        var suite = await _db.TestSuites
            .AsNoTracking()
            .Include(s => s.Scenarios).ThenInclude(ss => ss.Scenario)
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(TestSuite), request.Id);

        return TestSuiteDto.FromEntity(suite);
    }
}
