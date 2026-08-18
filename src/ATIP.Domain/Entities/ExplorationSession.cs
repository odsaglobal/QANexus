using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A single automated browser-based exploration of an application. The Explorer Agent
/// navigates the app, discovers pages and elements, and stores everything it finds here.
/// </summary>
public class ExplorationSession : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public Guid EnvironmentId { get; set; }

    public Environment Environment { get; set; } = null!;

    public ExplorationStatus Status { get; set; } = ExplorationStatus.Pending;

    /// <summary>Optional feature scope: when set, the crawl is driven only by this feature's manual/imported scenarios.</summary>
    public Guid? FeatureId { get; set; }

    /// <summary>Optional single-scenario scope: when set, the run walks and records results for just this scenario.</summary>
    public Guid? ScenarioId { get; set; }

    /// <summary>Optional suite scope: when set, the run walks every scenario in the suite sequentially in one browser session.</summary>
    public Guid? SuiteId { get; set; }

    /// <summary>
    /// Optional free-text mission. When set, the Explorer Agent autonomously performs the described
    /// flow (deciding each browser action itself) and records what it did as a new reusable scenario.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>Set to the scenario the agent recorded from a prompt-driven exploration, if any.</summary>
    public Guid? RecordedScenarioId { get; set; }

    /// <summary>
    /// When true, a scenario-scoped session EXECUTES the scenario's saved steps in order as a test
    /// (recording Passed/Healed/Failed per step) instead of the goal-driven exploration that discovers
    /// and proposes new steps. Set by the "Run" action; false for "Explore".
    /// </summary>
    public bool ExecuteSavedSteps { get; set; }

    /// <summary>Override the environment base URL; defaults to the environment's base URL.</summary>
    public string? SeedUrl { get; set; }

    public int MaxPages { get; set; } = 30;

    public int MaxDepth { get; set; } = 4;

    public int PagesDiscovered { get; set; }

    public int ElementsDiscovered { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<DiscoveredPage> Pages { get; set; } = new List<DiscoveredPage>();
}
