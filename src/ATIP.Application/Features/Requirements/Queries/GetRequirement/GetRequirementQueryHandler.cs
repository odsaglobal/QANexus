using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Requirements.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Requirements.Queries.GetRequirement;

public sealed class GetRequirementQueryHandler : IRequestHandler<GetRequirementQuery, RequirementDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetRequirementQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<RequirementDetailDto> Handle(
        GetRequirementQuery request,
        CancellationToken cancellationToken)
    {
        var requirement = await _db.Requirements
            .AsNoTracking()
            .Include(r => r.Modules).ThenInclude(m => m.Features).ThenInclude(f => f.UserStories)
            .Include(r => r.Modules).ThenInclude(m => m.Features).ThenInclude(f => f.Scenarios)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Requirement), request.Id);

        return RequirementDetailDto.FromEntity(requirement);
    }
}
