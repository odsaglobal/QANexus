using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Common;
using ATIP.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ATIP.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef migrations</c> to instantiate <see cref="ApplicationDbContext"/> at design
/// time by supplying the connection string from configuration and no-op runtime dependencies.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=atip;Username=atip;Password=atip";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        var clock = new SystemDateTimeProvider();
        var currentUser = new DesignTimeCurrentUser();
        var interceptor = new AuditableEntitySaveChangesInterceptor(currentUser, clock);

        return new ApplicationDbContext(options, currentUser, interceptor);
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public string? Email => null;
        public SystemRole? SystemRole => null;
        public string? IpAddress => null;
        public bool IsAuthenticated => false;
    }
}
