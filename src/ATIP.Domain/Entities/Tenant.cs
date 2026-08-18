using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// Root of the multi-tenant hierarchy. A tenant represents a customer organization;
/// all projects, users and data are isolated beneath it.
/// </summary>
public class Tenant : AuditableEntity, ISoftDeletable
{
    /// <summary>Human-readable organization name.</summary>
    public required string Name { get; set; }

    /// <summary>URL-safe unique identifier used in routes and invitations (e.g. "acme-corp").</summary>
    public required string Slug { get; set; }

    /// <summary>Optional external identity-provider (Azure AD) tenant/directory id.</summary>
    public string? ExternalDirectoryId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
