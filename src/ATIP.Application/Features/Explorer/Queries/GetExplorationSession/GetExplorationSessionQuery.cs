using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Queries.GetExplorationSession;

public sealed record GetExplorationSessionQuery(Guid Id) : IRequest<ExplorationSessionDto>;

public sealed class GetExplorationSessionQueryHandler
    : IRequestHandler<GetExplorationSessionQuery, ExplorationSessionDto>
{
    private readonly IApplicationDbContext _db;

    public GetExplorationSessionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ExplorationSessionDto> Handle(
        GetExplorationSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _db.ExplorationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ExplorationSession), request.Id);

        return ExplorationSessionDto.FromEntity(session);
    }
}
