using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Notifications.Dtos;

/// <summary>An in-app notification for the notification bell.</summary>
public sealed record NotificationDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Level { get; init; }
    public required string Category { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required bool IsRead { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public static NotificationDto FromEntity(Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        Level = n.Level,
        Category = n.Category,
        EntityType = n.EntityType,
        EntityId = n.EntityId,
        IsRead = n.IsRead,
        CreatedAtUtc = n.CreatedAtUtc,
    };
}
