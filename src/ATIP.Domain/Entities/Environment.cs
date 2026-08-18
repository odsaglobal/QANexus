using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A deployment target of the application under test (e.g. Staging at https://staging.acme.com).
/// Explorations and executions always run against a specific environment so that discovered
/// application knowledge and results are attributable to a known deployment.
/// </summary>
public class Environment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public required string Name { get; set; }

    public EnvironmentType Type { get; set; } = EnvironmentType.Test;

    /// <summary>Base URL of the application under test for this environment.</summary>
    public required string BaseUrl { get; set; }

    /// <summary>When true, this environment is used by default for new explorations/executions.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Arbitrary key/value variables (e.g. feature flags) serialized as JSON.</summary>
    public string? VariablesJson { get; set; }

    public ICollection<BrowserProfile> BrowserProfiles { get; set; } = new List<BrowserProfile>();
}
