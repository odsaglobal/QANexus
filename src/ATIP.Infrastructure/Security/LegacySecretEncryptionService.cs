using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Security;

/// <summary>
/// One-shot startup pass that encrypts environment secrets saved before secrets were encrypted
/// at rest. Idempotent: an environment with no plaintext secret is left untouched.
/// </summary>
internal sealed class LegacySecretEncryptionService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LegacySecretEncryptionService> _logger;

    public LegacySecretEncryptionService(IServiceScopeFactory scopeFactory, ILogger<LegacySecretEncryptionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

            // VariablesJson is jsonb, so string predicates don't translate; the table is small.
            var environments = await db.Environments
                .IgnoreQueryFilters()
                .Where(e => e.VariablesJson != null)
                .ToListAsync(cancellationToken);

            var migrated = 0;
            foreach (var environment in environments)
            {
                var stored = EnvironmentVariableSerialization.ParseStored(environment.VariablesJson);
                if (!stored.Any(v => v.IsSecret && v.Secret is null && !string.IsNullOrEmpty(v.Value)))
                {
                    continue;
                }

                environment.VariablesJson = EnvironmentVariableSerialization.SerializeStored(stored, protector);
                migrated++;
            }

            if (migrated > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Encrypted plaintext secret variables in {Count} environment(s).", migrated);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not encrypt legacy plaintext environment secrets.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
