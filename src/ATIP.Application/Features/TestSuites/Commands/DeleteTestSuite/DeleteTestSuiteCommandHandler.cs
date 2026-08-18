using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestSuites.Commands.DeleteTestSuite;

public sealed class DeleteTestSuiteCommandHandler : IRequestHandler<DeleteTestSuiteCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteTestSuiteCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteTestSuiteCommand request, CancellationToken cancellationToken)
    {
        var suite = await _db.TestSuites
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(TestSuite), request.Id);

        // Soft delete is applied by the SaveChanges interceptor when IsDeleted flips to true.
        suite.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
