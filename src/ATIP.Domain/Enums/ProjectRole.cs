namespace ATIP.Domain.Enums;

/// <summary>Project-scoped role that governs what a member may do within a single project.</summary>
public enum ProjectRole
{
    /// <summary>Read-only access to project data and reports.</summary>
    Viewer = 0,

    /// <summary>May author requirements, scenarios and run explorations/executions.</summary>
    Editor = 1,

    /// <summary>May manage members, environments, credentials and settings.</summary>
    Owner = 2
}
