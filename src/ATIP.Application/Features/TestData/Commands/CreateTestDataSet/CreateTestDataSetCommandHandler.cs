using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestData.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestData.Commands.CreateTestDataSet;

public sealed class CreateTestDataSetCommandHandler
    : IRequestHandler<CreateTestDataSetCommand, TestDataSetDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateTestDataSetCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TestDataSetDto> Handle(
        CreateTestDataSetCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var environment = await _db.Environments
            .FirstOrDefaultAsync(
                e => e.Id == request.EnvironmentId && e.ProjectId == request.ProjectId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Environment), request.EnvironmentId);

        var normalizedColumns = NormalizeColumns(request.Columns);
        var normalizedRows = NormalizeRows(request.Rows, normalizedColumns);

        var dataSet = new TestDataSet
        {
            TenantId = tenantId,
            ProjectId = environment.ProjectId,
            EnvironmentId = environment.Id,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ColumnsJson = TestDataSetDto.SerializeColumns(normalizedColumns),
            RowsJson = TestDataSetDto.SerializeRows(normalizedRows)
        };

        _db.TestDataSets.Add(dataSet);
        await _db.SaveChangesAsync(cancellationToken);

        return TestDataSetDto.FromEntity(dataSet);
    }

    internal static List<string> NormalizeColumns(IEnumerable<string> columns)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in columns)
        {
            var name = raw?.Trim();
            if (string.IsNullOrEmpty(name) || !seen.Add(name))
            {
                continue;
            }
            result.Add(name);
        }
        return result;
    }

    internal static List<Dictionary<string, string?>> NormalizeRows(
        IEnumerable<Dictionary<string, string?>> rows,
        IReadOnlyList<string> columns)
    {
        var result = new List<Dictionary<string, string?>>();
        foreach (var row in rows)
        {
            var normalized = new Dictionary<string, string?>();
            foreach (var column in columns)
            {
                row.TryGetValue(column, out var value);
                normalized[column] = value;
            }
            result.Add(normalized);
        }
        return result;
    }
}
