using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// Join entity assigning a <see cref="User"/> to a <see cref="Project"/> with a project-scoped role.
/// This is the unit that RBAC checks resolve against for project operations.
/// </summary>
public class ProjectMember : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public ProjectRole Role { get; set; } = ProjectRole.Viewer;
}
