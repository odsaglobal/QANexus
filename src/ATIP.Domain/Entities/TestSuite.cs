using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// A named, ordered collection of scenarios that can be executed together as a single run
/// against a chosen environment. Suites let teams group related scenarios (e.g. "Checkout regression")
/// and run them sequentially in one browser session so state carries over between scenarios.
/// </summary>
public class TestSuite : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>The scenarios in this suite, in execution order.</summary>
    public ICollection<TestSuiteScenario> Scenarios { get; set; } = new List<TestSuiteScenario>();
}
