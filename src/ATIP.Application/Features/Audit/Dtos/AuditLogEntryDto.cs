using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Audit.Dtos;

/// <summary>A single audit-trail entry for display in the activity log.</summary>
public sealed record AuditLogEntryDto
{
    public required Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public required string Action { get; init; }
    public required string Category { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required string Summary { get; init; }
    public string? IpAddress { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }

    public static AuditLogEntryDto FromEntity(AuditLogEntry a) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        UserEmail = a.UserEmail,
        Action = a.Action,
        Category = a.Category,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        Summary = a.Summary,
        IpAddress = a.IpAddress,
        TimestampUtc = a.TimestampUtc,
    };
}
