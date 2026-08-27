using ATIP.Application.Features.Notifications.Commands.MarkNotificationsRead;
using ATIP.Application.Features.Notifications.Dtos;
using ATIP.Application.Features.Notifications.Queries.GetUnreadCount;
using ATIP.Application.Features.Notifications.Queries.ListNotifications;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>In-app notifications for the current tenant/user.</summary>
[Route("api/v1/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    /// <summary>Lists recent notifications, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new ListNotificationsQuery(take), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns the unread notification count.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken cancellationToken)
    {
        var count = await Mediator.Send(new GetUnreadCountQuery(), cancellationToken);
        return Ok(count);
    }

    /// <summary>Marks a single notification read.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new MarkNotificationsReadCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Marks all of the caller's notifications read.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await Mediator.Send(new MarkNotificationsReadCommand(null), cancellationToken);
        return NoContent();
    }
}
