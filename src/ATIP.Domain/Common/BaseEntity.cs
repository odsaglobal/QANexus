namespace ATIP.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Uses a GUID surrogate key so identifiers
/// can be generated client-side and remain stable across a multi-tenant SaaS deployment.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
