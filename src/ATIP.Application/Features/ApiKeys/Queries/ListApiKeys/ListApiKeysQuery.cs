using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.ApiKeys.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.ApiKeys.Queries.ListApiKeys;

/// <summary>Lists the current tenant's API keys (no secrets), newest first.</summary>
public sealed record ListApiKeysQuery : IRequest<IReadOnlyList<ApiKeyDto>>;

public sealed class ListApiKeysQueryHandler : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeyDto>>
{
    private readonly IApplicationDbContext _db;

    public ListApiKeysQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApiKeyDto>> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var keys = await _db.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return keys.Select(ApiKeyDto.FromEntity).ToList();
    }
}
