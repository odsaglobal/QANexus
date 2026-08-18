using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A person who can authenticate to the platform. Users belong to exactly one tenant.
/// Local credentials (<see cref="PasswordHash"/>) and federated Azure AD sign-in are both supported;
/// federated users have a null password hash.
/// </summary>
public class User : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>BCrypt hash for local accounts; null for federated (Azure AD) accounts.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Object id from the external identity provider, when federated.</summary>
    public string? ExternalObjectId { get; set; }

    public SystemRole SystemRole { get; set; } = SystemRole.Member;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();

    public bool IsFederated => ExternalObjectId is not null;
}
