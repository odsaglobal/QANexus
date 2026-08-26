using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestData.Commands.CreateTestDataSet;
using ATIP.Application.Features.TestData.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestData.Commands.UpdateTestDataSet;

public sealed class UpdateTestDataSetCommandHandler
    : IRequestHandler<UpdateTestDataSetCommand, TestDataSetDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateTestDataSetCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TestDataSetDto> Handle(
        UpdateTestDataSetCommand request,
        CancellationToken cancellationToken)
    {
        var dataSet = await _db.TestDataSets
            .FirstOrDefaultAsync(
                d => d.Id == request.Id && d.EnvironmentId == request.EnvironmentId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(TestDataSet), request.Id);

        var normalizedColumns = CreateTestDataSetCommandHandler.NormalizeColumns(request.Columns);
        var normalizedRows = CreateTestDataSetCommandHandler.NormalizeRows(request.Rows, normalizedColumns);

        dataSet.Name = request.Name.Trim();
        dataSet.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        dataSet.ColumnsJson = TestDataSetDto.SerializeColumns(normalizedColumns);
        dataSet.RowsJson = TestDataSetDto.SerializeRows(normalizedRows);

        await _db.SaveChangesAsync(cancellationToken);

        return TestDataSetDto.FromEntity(dataSet);
    }
}
