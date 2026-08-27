using System.Security.Claims;
using System.Text.Encodings.Web;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Api.Services;

/// <summary>
/// Authenticates requests carrying an <c>X-Api-Key</c> header. Valid keys resolve to their owning
/// tenant and creating user, letting CI systems call the same endpoints as interactive users.
/// The API-key identity acts as a tenant admin within its tenant.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock)
        : base(options, logger, encoder)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return AuthenticateResult.NoResult(); // Let other schemes (JWT) handle it.
        }

        var hash = ApiKeyHashing.Hash(provided.ToString().Trim());

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var key = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyHash == hash);

        if (key is null || !key.IsActive)
        {
            return AuthenticateResult.Fail("Invalid or inactive API key.");
        }

        // Best-effort last-used stamp.
        try
        {
            key.LastUsedAtUtc = _clock.UtcNow;
            await db.SaveChangesAsync();
        }
        catch
        {
            // ignore
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim("sub", $"apikey:{key.Id}"));
        identity.AddClaim(new Claim(Auth0ClaimsTransformer.TenantIdClaim, key.TenantId.ToString()));
        identity.AddClaim(new Claim(Auth0ClaimsTransformer.AppUserIdClaim, key.CreatedByUserId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, SystemRole.TenantAdmin.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Email, $"apikey:{key.Name}"));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
