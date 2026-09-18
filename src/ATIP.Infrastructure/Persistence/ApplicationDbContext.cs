using System.Linq.Expressions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Common;
using ATIP.Domain.Entities;
using ATIP.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

    public DbSet<UiElement> UiElements => Set<UiElement>();

    public DbSet<UiElementLocator> UiElementLocators => Set<UiElementLocator>();

    public DbSet<DataConnection> DataConnections => Set<DataConnection>();

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
        ApplyOwnershipForeignKeys(modelBuilder);
        ApplyGlobalFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Backs every <c>TenantId</c>/<c>ProjectId</c> column with a real foreign key. Most entities
    /// carry the id without a navigation property, so convention never created the constraint and
    /// nothing at the database level stopped a row from outliving its owner. Entities that do
    /// declare a navigation already have the relationship and are skipped.
    /// </summary>
    private static void ApplyOwnershipForeignKeys(ModelBuilder modelBuilder)
    {
        // Materialised because configuring a relationship mutates the model being iterated.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            AddOwnerForeignKey(modelBuilder, entityType, typeof(Tenant), nameof(ITenantScoped.TenantId));
            AddOwnerForeignKey(modelBuilder, entityType, typeof(Project), "ProjectId");
        }
    }

    private static void AddOwnerForeignKey(
        ModelBuilder modelBuilder,
        IMutableEntityType entityType,
        Type ownerType,
        string foreignKeyName)
    {
        if (entityType.ClrType == ownerType || entityType.FindProperty(foreignKeyName) is null)
        {
            return;
        }

        // A navigation property (or an explicit configuration) already produced the constraint.
        // Adding a second one here would create a duplicate FK and a shadow "TenantId1" column.
        if (entityType.GetForeignKeys().Any(fk => fk.PrincipalEntityType.ClrType == ownerType))
        {
            return;
        }

        // Restrict, not the Cascade that EF defaults to for a required relationship: tenants and
        // projects are only ever soft-deleted, so a hard delete here would mean something has gone
        // wrong, and cascading it would silently erase every row belonging to that owner.
        modelBuilder.Entity(entityType.ClrType)
            .HasOne(ownerType)
            .WithMany()
            .HasForeignKey(foreignKeyName)
            .OnDelete(DeleteBehavior.Restrict);
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
                // Every read of this entity carries a TenantId predicate from the filter below, so
                // the column needs an index or each one degrades into a sequential scan. Declared
                // here rather than per-configuration so new tenant-scoped entities cannot forget it.
                // EF identifies an index by its property set, so this is a no-op where a
                // configuration already declares one.
                modelBuilder.Entity(clrType).HasIndex(nameof(ITenantScoped.TenantId));

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
