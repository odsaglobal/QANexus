namespace ATIP.Domain.Common;

/// <summary>
/// Marker for entities that belong to a tenant. Every tenant-scoped query is
/// filtered by <see cref="TenantId"/> via an EF Core global query filter to
/// guarantee isolation between organizations in the SaaS deployment.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
