namespace ATIP.Domain.Common;

/// <summary>
/// Adds audit columns populated automatically by the persistence layer's
/// <c>SaveChanges</c> interceptor. All timestamps are stored in UTC.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }
}
