using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Models;
using ATIP.Application.Features.Audit.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Audit.Queries.ListAuditLogs;

/// <summary>Lists the current tenant's audit trail, newest first (tenant-scoped by global filters).</summary>
public sealed record ListAuditLogsQuery(int Page = 1, int PageSize = 50, string? Category = null)
    : IRequest<PagedResult<AuditLogEntryDto>>;

public sealed class ListAuditLogsQueryHandler
    : IRequestHandler<ListAuditLogsQuery, PagedResult<AuditLogEntryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListAuditLogsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<AuditLogEntryDto>> Handle(
        ListAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(a => a.Category == request.Category);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => AuditLogEntryDto.FromEntity(a))
            .ToListAsync(cancellationToken);

        return PagedResult<AuditLogEntryDto>.Create(items, page, pageSize, total);
    }
}
