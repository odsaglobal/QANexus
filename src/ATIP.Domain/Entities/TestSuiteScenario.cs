using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>Join entity placing a <see cref="Scenario"/> at a specific position within a <see cref="TestSuite"/>.</summary>
public class TestSuiteScenario : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid SuiteId { get; set; }

    public TestSuite Suite { get; set; } = null!;

    public Guid ScenarioId { get; set; }

    public Scenario Scenario { get; set; } = null!;

    /// <summary>1-based execution position of the scenario within its suite.</summary>
    public int Order { get; set; }
}
