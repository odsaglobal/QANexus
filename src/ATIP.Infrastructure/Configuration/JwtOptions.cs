namespace ATIP.Infrastructure.Configuration;

/// <summary>Options controlling JWT issuance and validation, bound from configuration section "Jwt".</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "atip";

    public string Audience { get; set; } = "atip-clients";

    /// <summary>Symmetric signing key. In production this must be supplied via a secret store, not appsettings.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
}
