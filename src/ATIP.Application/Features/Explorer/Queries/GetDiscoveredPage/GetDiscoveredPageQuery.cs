using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.GetDiscoveredPage;

public sealed record GetDiscoveredPageQuery(Guid Id) : IRequest<DiscoveredPageDetailDto>;

public sealed class GetDiscoveredPageQueryHandler
    : IRequestHandler<GetDiscoveredPageQuery, DiscoveredPageDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetDiscoveredPageQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<DiscoveredPageDetailDto> Handle(
        GetDiscoveredPageQuery request, CancellationToken cancellationToken)
    {
        var page = await _db.DiscoveredPages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DiscoveredPage), request.Id);

        return DiscoveredPageDetailDto.FromEntity(page);
    }
}
