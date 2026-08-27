using System.Linq.Expressions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Common;
using ATIP.Domain.Entities;
using ATIP.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Infrastructure.Persistence;

/// <summary>
/// EF Core context and the concrete <see cref="IApplicationDbContext"/>. Applies two global
/// query filters to every tenant-scoped, soft-deletable entity:
/// <list type="bullet">
///   <item>tenant isolation — rows are only visible to their owning tenant;</item>
///   <item>soft delete — logically deleted rows are hidden.</item>
/// </list>
/// Handlers that must bypass these (e.g. login) call <c>IgnoreQueryFilters()</c> explicitly.
/// </summary>
public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly AuditableEntitySaveChangesInterceptor _auditInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser,
        AuditableEntitySaveChangesInterceptor auditInterceptor)
        : base(options)
    {
        _currentUser = currentUser;
        _auditInterceptor = auditInterceptor;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<EnvEntity> Environments => Set<EnvEntity>();

    public DbSet<BrowserProfile> BrowserProfiles => Set<BrowserProfile>();

    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<TestDataSet> TestDataSets => Set<TestDataSet>();

    public DbSet<Requirement> Requirements => Set<Requirement>();

    public DbSet<RequirementModule> RequirementModules => Set<RequirementModule>();

    public DbSet<Feature> Features => Set<Feature>();

    public DbSet<UserStory> UserStories => Set<UserStory>();

    public DbSet<Scenario> Scenarios => Set<Scenario>();

    public DbSet<ScenarioStep> ScenarioSteps => Set<ScenarioStep>();

    public DbSet<ScenarioStepResult> ScenarioStepResults => Set<ScenarioStepResult>();

    public DbSet<ExplorationSession> ExplorationSessions => Set<ExplorationSession>();

    public DbSet<DiscoveredPage> DiscoveredPages => Set<DiscoveredPage>();

    public DbSet<DiscoveredElement> DiscoveredElements => Set<DiscoveredElement>();

    public DbSet<ElementLocator> ElementLocators => Set<ElementLocator>();

    public DbSet<TestSuite> TestSuites => Set<TestSuite>();

    public DbSet<TestSuiteScenario> TestSuiteScenarios => Set<TestSuiteScenario>();

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable)
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "e");
            Expression? body = null;

            if (isTenantScoped)
            {
                // e => !e.TenantId.HasValue-scenario handled by comparing to current tenant.
                var tenantProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var currentTenant = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentTenantId));
                body = Expression.Equal(tenantProperty, currentTenant);
            }

            if (isSoftDeletable)
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var notDeleted = Expression.Not(isDeletedProperty);
                body = body is null ? notDeleted : Expression.AndAlso(body, notDeleted);
            }

            var lambda = Expression.Lambda(body!, parameter);
            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }

    /// <summary>
    /// Current tenant id used by the global tenant filter. Resolves to <see cref="Guid.Empty"/>
    /// for unauthenticated contexts so that no tenant-scoped rows leak before sign-in.
    /// Referenced by the compiled filter expression, so it must remain a public instance member.
    /// </summary>
    public Guid CurrentTenantId => _currentUser.TenantId ?? Guid.Empty;
}
