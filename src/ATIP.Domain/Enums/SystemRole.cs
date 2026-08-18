namespace ATIP.Domain.Enums;

/// <summary>
/// Coarse-grained platform roles. Fine-grained, project-scoped permissions are layered
/// on top via <see cref="Entities.ProjectMember"/> and <see cref="ProjectRole"/>.
/// </summary>
public enum SystemRole
{
    /// <summary>Full control over a single tenant (organization).</summary>
    TenantAdmin = 0,

    /// <summary>Standard authenticated member of a tenant.</summary>
    Member = 1,

    /// <summary>Cross-tenant operator; reserved for platform operations staff.</summary>
    PlatformAdmin = 2
}
