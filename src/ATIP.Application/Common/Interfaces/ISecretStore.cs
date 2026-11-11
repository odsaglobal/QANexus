using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// An external secret manager (HashiCorp Vault today). Values are fetched at run time only and are
/// never persisted, logged or returned through the API.
/// </summary>
public interface ISecretStore
{
    string Provider { get; }

    bool IsConfigured { get; }

    /// <summary>
    /// Reads one field of a secret. <paramref name="reference"/> is <c>path#field</c>, resolved inside
    /// the tenant's own namespace, so one tenant can never address another tenant's secrets.
    /// </summary>
    /// <exception cref="Exceptions.SecretStoreException">The secret cannot be read.</exception>
    Task<string> GetSecretAsync(Guid tenantId, string reference, CancellationToken cancellationToken = default);

    Task<SecretStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed record SecretStoreStatus(string Provider, bool Configured, bool Reachable, bool Authenticated, string? Detail);

/// <summary>A parsed <c>path#field</c> reference into the secret store.</summary>
public sealed partial record SecretReference(string Path, string Field)
{
    public const string DefaultField = "value";

    public static bool TryParse(string? text, [NotNullWhen(true)] out SecretReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var hash = trimmed.IndexOf('#');
        var path = (hash < 0 ? trimmed : trimmed[..hash]).Trim('/');
        var field = hash < 0 ? DefaultField : trimmed[(hash + 1)..];

        if (!PathPattern().IsMatch(path) || path.Split('/').Any(s => s is "" or "." or "..") || !FieldPattern().IsMatch(field))
        {
            return false;
        }

        reference = new SecretReference(path, field);
        return true;
    }

    public override string ToString() => $"{Path}#{Field}";

    [GeneratedRegex(@"^[A-Za-z0-9._\-/]{1,256}$")]
    private static partial Regex PathPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._\-]{1,128}$")]
    private static partial Regex FieldPattern();
}
