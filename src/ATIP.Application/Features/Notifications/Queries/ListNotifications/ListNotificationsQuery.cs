using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Notifications.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Notifications.Queries.ListNotifications;

/// <summary>Lists the current tenant/user's notifications, newest first.</summary>
public sealed record ListNotificationsQuery(int Take = 30) : IRequest<IReadOnlyList<NotificationDto>>;

public sealed class ListNotificationsQueryHandler
    : IRequestHandler<ListNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ListNotificationsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(
        ListNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var take = Math.Clamp(request.Take, 1, 100);

        var items = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == null || n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => NotificationDto.FromEntity(n))
            .ToListAsync(cancellationToken);

        return items;
    }
}
