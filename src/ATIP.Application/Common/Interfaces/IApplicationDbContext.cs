using ATIP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the persistence context exposed to the Application layer. Keeping the
/// concrete <c>DbContext</c> in Infrastructure preserves the dependency rule while still
/// allowing handlers to compose LINQ queries directly against the model.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<Project> Projects { get; }

    DbSet<Domain.Entities.Environment> Environments { get; }

    DbSet<BrowserProfile> BrowserProfiles { get; }

    DbSet<Credential> Credentials { get; }

    DbSet<ProjectMember> ProjectMembers { get; }

    DbSet<TestDataSet> TestDataSets { get; }

    DbSet<Requirement> Requirements { get; }

    DbSet<RequirementModule> RequirementModules { get; }

    DbSet<Feature> Features { get; }

    DbSet<UserStory> UserStories { get; }

    DbSet<Scenario> Scenarios { get; }

    DbSet<ScenarioStep> ScenarioSteps { get; }

    DbSet<ScenarioStepResult> ScenarioStepResults { get; }

    DbSet<ExplorationSession> ExplorationSessions { get; }

    DbSet<DiscoveredPage> DiscoveredPages { get; }

    DbSet<DiscoveredElement> DiscoveredElements { get; }

    DbSet<ElementLocator> ElementLocators { get; }

    DbSet<TestSuite> TestSuites { get; }

    DbSet<TestSuiteScenario> TestSuiteScenarios { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
