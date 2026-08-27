using ATIP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Notifications.Queries.GetUnreadCount;

/// <summary>Returns the number of unread notifications for the current tenant/user.</summary>
public sealed record GetUnreadCountQuery : IRequest<int>;

public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetUnreadCountQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        return await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => !n.IsRead && (n.UserId == null || n.UserId == userId), cancellationToken);
    }
}
