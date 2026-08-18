using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestSuites.Commands;
using ATIP.Application.Features.TestSuites.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Commands.UpdateTestSuite;

public sealed class UpdateTestSuiteCommandHandler : IRequestHandler<UpdateTestSuiteCommand, TestSuiteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateTestSuiteCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TestSuiteDto> Handle(UpdateTestSuiteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var suite = await _db.TestSuites
            .Include(s => s.Scenarios)
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(TestSuite), request.Id);

        suite.Name = request.Name.Trim();
        suite.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await TestSuiteScenarioSync.ReplaceScenariosAsync(_db, suite, request.ScenarioIds, tenantId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var reloaded = await _db.TestSuites
            .AsNoTracking()
            .Include(s => s.Scenarios).ThenInclude(ss => ss.Scenario)
            .FirstAsync(s => s.Id == suite.Id, cancellationToken);

        return TestSuiteDto.FromEntity(reloaded);
    }
}
