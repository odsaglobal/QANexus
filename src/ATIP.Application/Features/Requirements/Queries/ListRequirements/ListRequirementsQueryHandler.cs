using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Requirements.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Requirements.Queries.ListRequirements;

public sealed class ListRequirementsQueryHandler
    : IRequestHandler<ListRequirementsQuery, IReadOnlyList<RequirementDto>>
{
    private readonly IApplicationDbContext _db;

    public ListRequirementsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<RequirementDto>> Handle(
        ListRequirementsQuery request,
        CancellationToken cancellationToken)
    {
        var requirements = await _db.Requirements
            .AsNoTracking()
            .Include(r => r.Modules).ThenInclude(m => m.Features)
            .Where(r => r.ProjectId == request.ProjectId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return requirements.Select(RequirementDto.FromEntity).ToList();
    }
}
