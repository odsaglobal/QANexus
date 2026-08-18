namespace ATIP.Domain.Common;

/// <summary>
/// Marks an entity that supports soft deletion. Rows are never physically removed;
/// instead <see cref="IsDeleted"/> is flipped and hidden by a global query filter,
/// preserving audit history and traceability required by enterprise QA workflows.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAtUtc { get; set; }
}
