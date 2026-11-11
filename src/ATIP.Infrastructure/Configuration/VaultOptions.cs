namespace ATIP.Infrastructure.Configuration;

/// <summary>HashiCorp Vault connection, bound from section "Vault". Only KV v2 is supported.</summary>
public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    public bool Enabled { get; set; }

    /// <summary>e.g. <c>https://vault.example.com:8200</c>.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Vault Enterprise / HCP namespace. Leave empty for open-source Vault.</summary>
    public string? Namespace { get; set; }

    /// <summary>Mount path of the KV v2 engine.</summary>
    public string MountPath { get; set; } = "secret";

    /// <summary>
    /// Prepended to every reference so a tenant can only read under its own folder.
    /// <c>{tenantId}</c> is substituted per request.
    /// </summary>
    public string PathPrefix { get; set; } = "patternsai/{tenantId}";

    /// <summary><c>approle</c> (recommended) or <c>token</c>.</summary>
    public string AuthMethod { get; set; } = "approle";

    public string? Token { get; set; }

    public string? RoleId { get; set; }

    public string? SecretId { get; set; }

    public string AppRoleMountPath { get; set; } = "approle";

    /// <summary>How long a read secret is kept in memory. 0 disables caching.</summary>
    public int CacheSeconds { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 10;

    public bool UsesAppRole => string.Equals(AuthMethod, "approle", StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured =>
        Enabled
        && Uri.TryCreate(Address, UriKind.Absolute, out _)
        && (UsesAppRole
            ? !string.IsNullOrWhiteSpace(RoleId) && !string.IsNullOrWhiteSpace(SecretId)
            : !string.IsNullOrWhiteSpace(Token));
}
