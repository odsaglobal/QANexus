using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.DataConnections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.DataConnections.Queries.ListDataConnections;

public sealed class ListDataConnectionsQueryHandler
    : IRequestHandler<ListDataConnectionsQuery, IReadOnlyList<DataConnectionDto>>
{
    private readonly IApplicationDbContext _db;

    public ListDataConnectionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DataConnectionDto>> Handle(
        ListDataConnectionsQuery request,
        CancellationToken cancellationToken)
    {
        var connections = await _db.DataConnections
            .AsNoTracking()
            .Where(c => c.ProjectId == request.ProjectId
                && c.EnvironmentId == request.EnvironmentId
                && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return connections.Select(DataConnectionDto.FromEntity).ToList();
    }
}
