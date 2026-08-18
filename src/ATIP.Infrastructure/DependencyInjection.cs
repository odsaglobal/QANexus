using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure.Ai;
using ATIP.Infrastructure.Common;
using ATIP.Infrastructure.Configuration;
using ATIP.Infrastructure.Documents;
using ATIP.Infrastructure.Exploration;
using ATIP.Infrastructure.Identity;
using ATIP.Infrastructure.Persistence;
using ATIP.Infrastructure.Persistence.Interceptors;
using ATIP.Infrastructure.Security;
using ATIP.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ATIP.Infrastructure;

/// <summary>Registers persistence, identity, security and supporting services for the Infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<EncryptionOptions>()
            .Bind(configuration.GetSection(EncryptionOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName));

        services.AddOptions<PlaywrightMcpOptions>()
            .Bind(configuration.GetSection(PlaywrightMcpOptions.SectionName));

        services.AddOptions<TestRailOptions>()
            .Bind(configuration.GetSection(TestRailOptions.SectionName));

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            }));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

        // AI, documents and storage.
        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()
            ?? configuration.GetSection(LlmOptions.ResolverSectionName).Get<LlmOptions>()
            ?? new LlmOptions();

        var isAzure = llmOptions.IsAzure;

        services.AddHttpClient("llm", client =>
        {
            // The absolute chat-completions URL is resolved per-request from LlmOptions.ChatCompletionsUrl,
            // so no BaseAddress is required here. We only configure auth and timeout.
            if (!string.IsNullOrWhiteSpace(llmOptions.ApiKey))
            {
                if (isAzure)
                {
                    client.DefaultRequestHeaders.Add("api-key", llmOptions.ApiKey);
                }
                else
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", llmOptions.ApiKey);
                }
            }

            client.Timeout = TimeSpan.FromSeconds(llmOptions.TimeoutSeconds);
        });

        var testRailOptions = configuration.GetSection(TestRailOptions.SectionName).Get<TestRailOptions>() ?? new TestRailOptions();
        services.AddHttpClient("testrail", client =>
        {
            if (!string.IsNullOrWhiteSpace(testRailOptions.BaseUrl))
            {
                var baseUrl = testRailOptions.BaseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(baseUrl);
            }

            if (!string.IsNullOrWhiteSpace(testRailOptions.Username) && !string.IsNullOrWhiteSpace(testRailOptions.ApiKey))
            {
                var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{testRailOptions.Username}:{testRailOptions.ApiKey}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
            }

            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<ILlmClient, LlmClient>();
        services.AddScoped<ITestRailClient, TestRailClient>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Application Explorer: queue (singleton shared instance), agent (scoped), background service.
        var explorationQueue = new ExplorationQueue();
        services.AddSingleton(explorationQueue);
        services.AddSingleton<IExplorationQueue>(explorationQueue);
        services.AddScoped<IExplorerAgent, ExplorerAgent>();

        // Bounded concurrency so multiple users' explorations run independently in parallel.
        var maxParallelSessions = configuration.GetValue("Exploration:MaxParallelSessions", 4);
        services.AddHostedService(sp => new ExplorationBackgroundService(
            sp.GetRequiredService<ExplorationQueue>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            maxParallelSessions,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExplorationBackgroundService>>()));

        return services;
    }
}
