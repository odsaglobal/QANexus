using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestData.Commands.DeleteTestDataSet;

public sealed class DeleteTestDataSetCommandHandler : IRequestHandler<DeleteTestDataSetCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteTestDataSetCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteTestDataSetCommand request, CancellationToken cancellationToken)
    {
        var dataSet = await _db.TestDataSets
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TestDataSet), request.Id);

        _db.TestDataSets.Remove(dataSet);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
