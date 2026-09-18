using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Api.Services;

/// <summary>
/// Bridges an Auth0-authenticated principal into the platform's internal identity model.
/// Auth0 access tokens carry the external subject but not our internal tenant/user GUIDs (and only
/// carry email/name when a Login Action adds them as namespaced custom claims), so on the first
/// request for a given Auth0 user we JIT-provision an internal <see cref="User"/> (grouping users into
/// a <see cref="Tenant"/> by email domain), then enrich the principal with the internal
/// <c>tenant_id</c> / <c>app_user_id</c> / role claims that the rest of the app relies on.
/// When the token has no email claim we fall back to Auth0's <c>/userinfo</c> endpoint (the SPA
/// requests the <c>openid profile email</c> scopes) so members show their real email/name.
/// </summary>
public sealed class Auth0ClaimsTransformer : IClaimsTransformation
{
    public const string TenantIdClaim = "tenant_id";
    public const string AppUserIdClaim = "app_user_id";
    private const string SyntheticEmailSuffix = "@users.noreply";

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly Auth0Options _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Auth0ClaimsTransformer> _logger;

    public Auth0ClaimsTransformer(
        IApplicationDbContext db,
        IDateTimeProvider clock,
        IOptions<Auth0Options> options,
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        ILogger<Auth0ClaimsTransformer> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        // Idempotent: this transformer can run multiple times per request.
        if (principal.HasClaim(c => c.Type == TenantIdClaim))
        {
            return principal;
        }

        var externalId = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        var ns = _options.ClaimsNamespace;
        var email = principal.FindFirstValue(ns + "email")
                    ?? principal.FindFirstValue("email")
                    ?? principal.FindFirstValue(ClaimTypes.Email);
        var name = principal.FindFirstValue(ns + "name")
                   ?? principal.FindFirstValue("name")
                   ?? principal.FindFirstValue(ClaimTypes.Name)
                   ?? email;

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return principal;
        }

        // Auth0 Organizations: an org-scoped login carries these claims. When present they are the
        // authoritative tenant; otherwise we fall back to grouping users by email domain.
        var orgId = principal.FindFirstValue("org_id");
        var orgName = principal.FindFirstValue("org_name");

        var user = await ResolveOrProvisionAsync(externalId, email, name, orgId, orgName);

        identity.AddClaim(new Claim(TenantIdClaim, user.TenantId.ToString()));
        identity.AddClaim(new Claim(AppUserIdClaim, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.SystemRole.ToString()));
        if (!principal.HasClaim(c => c.Type == ClaimTypes.Email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        }

        return principal;
    }

    private async Task<User> ResolveOrProvisionAsync(string externalId, string? email, string? name, string? orgId = null, string? orgName = null)
    {
        // 1) Match an already-linked federated user by Auth0 subject.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ExternalObjectId == externalId);

        // When the token carries no email, look it up from Auth0's /userinfo — but only when it can
        // actually help (new user, or an existing record still stuck on a synthetic email).
        if (string.IsNullOrWhiteSpace(email) && (user is null || IsSynthetic(user.Email)))
        {
            var (uiEmail, uiName) = await TryFetchUserInfoAsync();
            if (!string.IsNullOrWhiteSpace(uiEmail))
            {
                email = uiEmail;
            }
            if (string.IsNullOrWhiteSpace(name) || IsSynthetic(name!))
            {
                name = uiName ?? name;
            }
        }

        // 2) Link a pre-existing local account with the same email to this Auth0 identity.
        if (user is null && !string.IsNullOrWhiteSpace(email))
        {
            user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email);
            if (user is not null && user.ExternalObjectId is null)
            {
                user.ExternalObjectId = externalId;
            }
        }

        // 3) JIT-provision a brand new user (and tenant).
        if (user is null)
        {
            // Fall back to a per-subject synthetic email when the token carries no email claim.
            var effectiveEmail = string.IsNullOrWhiteSpace(email)
                ? $"{Sanitize(externalId)}@users.noreply"
                : email;

            Tenant tenant;
            bool firstUserOfTenant;

            if (!string.IsNullOrWhiteSpace(orgId))
            {
                // Auth0 Organization login → the org is the tenant (one client = one Auth0 org).
                var existing = await _db.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.ExternalDirectoryId == orgId);

                firstUserOfTenant = existing is null;
                if (existing is null)
                {
                    var label = string.IsNullOrWhiteSpace(orgName) ? orgId! : orgName!;
                    tenant = new Tenant
                    {
                        Name = ToTitle(label),
                        Slug = $"{Slugify(label)}-{Guid.NewGuid().ToString("N")[..6]}",
                        ExternalDirectoryId = orgId,
                        // Org name is set by the Auth0 admin — treat it as already named.
                        IsOnboarded = true,
                    };
                    _db.Tenants.Add(tenant);
                }
                else
                {
                    tenant = existing;
                }
            }
            else
            {
                // No organization context → group users into a tenant by email domain.
                var domain = effectiveEmail.Split('@').LastOrDefault()?.ToLowerInvariant() ?? "workspace";

                var existing = await _db.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.ExternalDirectoryId == domain);

                firstUserOfTenant = existing is null;
                if (existing is null)
                {
                    tenant = new Tenant
                    {
                        Name = DefaultWorkspaceName(domain, name, effectiveEmail),
                        Slug = $"{Slugify(domain)}-{Guid.NewGuid().ToString("N")[..6]}",
                        ExternalDirectoryId = domain,
                        // Domain-derived name is only a guess → prompt the owner to name it.
                        IsOnboarded = false,
                    };
                    _db.Tenants.Add(tenant);
                }
                else
                {
                    tenant = existing;
                }
            }

            user = new User
            {
                TenantId = tenant.Id,
                Email = effectiveEmail,
                DisplayName = string.IsNullOrWhiteSpace(name) ? effectiveEmail : name!,
                ExternalObjectId = externalId,
                SystemRole = firstUserOfTenant ? SystemRole.TenantAdmin : SystemRole.Member,
            };
            _db.Users.Add(user);
        }
        else if (IsSynthetic(user.Email) && !string.IsNullOrWhiteSpace(email) && !IsSynthetic(email))
        {
            // Heal a previously-provisioned user whose email/name were placeholders.
            user.Email = email!;
            if (!string.IsNullOrWhiteSpace(name) && !IsSynthetic(name!))
            {
                user.DisplayName = name!;
            }
        }

        user.LastLoginAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);
        return user;
    }

    private static bool IsSynthetic(string value) =>
        value.EndsWith(SyntheticEmailSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Calls Auth0 <c>/userinfo</c> with the caller's bearer token to obtain email + name.</summary>
    private async Task<(string? Email, string? Name)> TryFetchUserInfoAsync()
    {
        var domain = _options.Domain;
        if (string.IsNullOrWhiteSpace(domain))
        {
            return (null, null);
        }

        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{domain}/userinfo");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auth0 /userinfo returned {Status}", (int)response.StatusCode);
                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var email = GetString(root, "email");
            var name = GetString(root, "name") ?? GetString(root, "nickname") ?? GetString(root, "given_name");
            _logger.LogInformation("Auth0 /userinfo ok: email={HasEmail}, name={HasName}",
                !string.IsNullOrWhiteSpace(email), !string.IsNullOrWhiteSpace(name));
            return (email, name);
        }
        catch (Exception ex)
        {
            // Best-effort enrichment; never fail the request over userinfo.
            _logger.LogWarning(ex, "Auth0 /userinfo call failed");
            return (null, null);
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;


    private static string ToTitle(string domain)
    {
        var root = domain.Split('.').FirstOrDefault() ?? domain;
        return string.IsNullOrEmpty(root)
            ? "Workspace"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(root);
    }

    private static readonly HashSet<string> ConsumerDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "users.noreply", "gmail.com", "googlemail.com", "outlook.com", "hotmail.com",
        "live.com", "yahoo.com", "yahoo.co.in", "icloud.com", "me.com", "proton.me",
        "protonmail.com", "aol.com", "gmx.com", "mail.com",
    };

    /// <summary>
    /// A best-guess default workspace name used only until the owner completes onboarding.
    /// For real company domains we title-case the domain (acme.com → "Acme"); for consumer or
    /// synthetic domains we fall back to "{Name}'s Workspace" (never leaking "users.noreply").
    /// </summary>
    private static string DefaultWorkspaceName(string domain, string? name, string effectiveEmail)
    {
        if (!ConsumerDomains.Contains(domain))
        {
            return ToTitle(domain);
        }

        var person = !string.IsNullOrWhiteSpace(name)
                     && !name!.EndsWith(SyntheticEmailSuffix, StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(name, effectiveEmail, StringComparison.OrdinalIgnoreCase)
            ? name
            : null;

        return person is not null ? $"{person}'s Workspace" : "My Workspace";
    }

    private static string Slugify(string domain)
    {
        var chars = domain.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
