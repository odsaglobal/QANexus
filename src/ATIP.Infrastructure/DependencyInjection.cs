using ATIP.Application.Common.Interfaces;
using ATIP.Application.Engine.Contracts;
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

        services.AddOptions<JiraOptions>()
            .Bind(configuration.GetSection(JiraOptions.SectionName));

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

        services.AddScoped<IAuditLogger, Audit.AuditLogger>();
        services.AddScoped<INotificationService, Notifications.NotificationService>();

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
                if (llmOptions.IsAnthropic)
                {
                    // Anthropic authenticates with x-api-key (NOT a Bearer token) and rejects any request
                    // that omits the API version header.
                    client.DefaultRequestHeaders.Add("x-api-key", llmOptions.ApiKey);
                    client.DefaultRequestHeaders.Add("anthropic-version", llmOptions.AnthropicVersion);
                }
                else if (isAzure)
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

        var jiraOptions = configuration.GetSection(JiraOptions.SectionName).Get<JiraOptions>() ?? new JiraOptions();
        services.AddHttpClient("jira", client =>
        {
            if (!string.IsNullOrWhiteSpace(jiraOptions.BaseUrl) && !jiraOptions.BaseUrl.Contains('<'))
            {
                client.BaseAddress = new Uri(jiraOptions.BaseUrl.TrimEnd('/') + "/");
            }

            if (!string.IsNullOrWhiteSpace(jiraOptions.Email) && !string.IsNullOrWhiteSpace(jiraOptions.ApiToken))
            {
                var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{jiraOptions.Email}:{jiraOptions.ApiToken}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
            }
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<ILlmClient, LlmClient>();
        services.AddScoped<ITestRailClient, TestRailClient>();
        services.AddScoped<IJiraClient, JiraClient>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Application Explorer: queue (singleton shared instance), agent (scoped), background service.
        var explorationQueue = new ExplorationQueue();
        services.AddSingleton(explorationQueue);
        services.AddSingleton<IExplorationQueue>(explorationQueue);
        services.AddScoped<IExplorerAgent, ExplorerAgent>();
        services.AddSingleton<IExplorationCancellationRegistry, ExplorationCancellationRegistry>();

        AddAutomationEngine(services, configuration);

        // Bounded concurrency so multiple users' explorations run independently in parallel.
        var maxParallelSessions = configuration.GetValue("Exploration:MaxParallelSessions", 4);
        // Watchdog: a hung agent (dead MCP subprocess, stalled LLM, infinite loop) must not hold its
        // concurrency slot — and the DB row — forever. Force it to Failed after this many minutes.
        var maxSessionMinutes = configuration.GetValue("Exploration:MaxSessionMinutes", 20);
        services.AddHostedService(sp => new ExplorationBackgroundService(
            sp.GetRequiredService<ExplorationQueue>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            maxParallelSessions,
            maxSessionMinutes,
            sp.GetRequiredService<IExplorationCancellationRegistry>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExplorationBackgroundService>>()));

        return services;
    }

    /// <summary>
    /// Registers the generic test engine: the object repository behind self-healing locators and
    /// one driver factory per platform.
    /// </summary>
    /// <remarks>
    /// Everything here is scoped to a run. The engine holds live drivers — a browser, an Appium
    /// session, open connections — so a singleton would have two concurrent runs sharing one
    /// browser page, and a transient would start a fresh browser for every single action.
    /// </remarks>
    private static void AddAutomationEngine(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Engine.Mobile.MobileDriverOptions>(
            configuration.GetSection(Engine.Mobile.MobileDriverOptions.SectionName));

        services.AddScoped<ILocatorRepository, Engine.Locators.LocatorRepository>();
        services.AddScoped<ILocatorResolver, Engine.Locators.LocatorResolver>();

        services.AddScoped<Engine.Web.WebBrowserSession>();

        services.AddScoped<ITestDriverFactory, Engine.WebDriverFactory>();
        services.AddScoped<ITestDriverFactory, Engine.ApiDriverFactory>();
        services.AddScoped<ITestDriverFactory, Engine.DatabaseDriverFactory>();
        services.AddScoped<ITestDriverFactory, Engine.MobileDriverFactory>();

        // Named clients so engine traffic gets its own handler pool and cannot inherit the
        // auth headers or base address configured for the product's own integrations.
        services.AddHttpClient("atip-test-api");
        services.AddHttpClient("atip-appium");

        services.AddScoped<ITestEngine, Engine.TestEngine>();
    }
}
