using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestSuites.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Commands.CreateTestSuite;

public sealed class CreateTestSuiteCommandHandler : IRequestHandler<CreateTestSuiteCommand, TestSuiteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateTestSuiteCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TestSuiteDto> Handle(CreateTestSuiteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var suite = new TestSuite
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        };

        await TestSuiteScenarioSync.ReplaceScenariosAsync(_db, suite, request.ScenarioIds, tenantId, cancellationToken);

        _db.TestSuites.Add(suite);
        await _db.SaveChangesAsync(cancellationToken);

        return TestSuiteDto.FromEntity(suite);
    }
}
