namespace ATIP.Infrastructure.Configuration;

/// <summary>
/// Options for validating Auth0 access tokens (RS256/JWKS), bound from configuration section "Auth0".
/// The API trusts access tokens issued by the configured Auth0 tenant for the given API audience and
/// JIT-provisions an internal user/tenant on first sign-in.
/// </summary>
public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    /// <summary>Auth0 tenant domain, e.g. <c>dev-abc123.us.auth0.com</c> (no scheme, no trailing slash).</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>The API Identifier configured in Auth0 (Applications → APIs), e.g. <c>https://qanexus-api</c>.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>The SPA client id (informational; the SPA also needs it in its own config).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Namespace for custom claims added by an Auth0 Login Action (Auth0 drops non-namespaced custom
    /// claims). The transformer reads <c>{ClaimsNamespace}email</c> / <c>{ClaimsNamespace}name</c>.
    /// </summary>
    public string ClaimsNamespace { get; set; } = "https://qanexus.app/";

    /// <summary>The OIDC issuer derived from the domain (Auth0 issuers always have a trailing slash).</summary>
    public string Issuer => string.IsNullOrWhiteSpace(Domain) ? string.Empty : $"https://{Domain}/";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Domain)
        && !Domain.Contains('<', StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(Audience)
        && !Audience.Contains('<', StringComparison.Ordinal);
}
