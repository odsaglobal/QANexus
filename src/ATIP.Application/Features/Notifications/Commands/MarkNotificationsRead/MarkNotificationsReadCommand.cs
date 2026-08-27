using ATIP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Notifications.Commands.MarkNotificationsRead;

/// <summary>Marks notifications read: a specific one when <see cref="Id"/> is set, else all for the caller.</summary>
public sealed record MarkNotificationsReadCommand(Guid? Id = null) : IRequest<int>;

public sealed class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MarkNotificationsReadCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var query = _db.Notifications
            .Where(n => !n.IsRead && (n.UserId == null || n.UserId == userId));

        if (request.Id is { } id)
        {
            query = query.Where(n => n.Id == id);
        }

        return await query.ExecuteUpdateAsync(
            s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }
}
