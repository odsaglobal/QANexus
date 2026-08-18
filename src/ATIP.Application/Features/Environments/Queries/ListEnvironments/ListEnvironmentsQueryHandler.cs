using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Environments.Queries.ListEnvironments;

public sealed class ListEnvironmentsQueryHandler
    : IRequestHandler<ListEnvironmentsQuery, IReadOnlyList<EnvironmentDto>>
{
    private readonly IApplicationDbContext _db;

    public ListEnvironmentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnvironmentDto>> Handle(
        ListEnvironmentsQuery request,
        CancellationToken cancellationToken)
    {
        var environments = await _db.Environments
            .AsNoTracking()
            .Where(e => e.ProjectId == request.ProjectId)
            .OrderBy(e => e.Type)
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);

        return environments.Select(EnvironmentDto.FromEntity).ToList();
    }
}
