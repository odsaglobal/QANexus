using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;

namespace ATIP.Application.Features.Environments.Dtos;

/// <summary>An environment variable as persisted. A secret keeps only ciphertext in <see cref="Secret"/>.</summary>
public sealed record StoredEnvironmentVariable(string Key, string Type, string? Value, ProtectedSecret? Secret)
{
    public bool IsSecret => Type == EnvironmentVariableSerialization.SecretType;

    /// <summary>The value is a <c>path#field</c> reference into the external secret store, not the secret.</summary>
    public bool IsVaultReference => Type == EnvironmentVariableSerialization.VaultType;

    public bool HasValue => Secret is not null || !string.IsNullOrEmpty(Value);
}

/// <summary>
/// Reads/writes an environment's key/value variables stored as JSON:
/// text <c>{ "key": { "value": "...", "type": "text" } }</c>,
/// secret <c>{ "key": { "type": "secret", "enc": "...", "nonce": "...", "keyId": "..." } }</c>,
/// vault <c>{ "key": { "value": "path#field", "type": "vault" } }</c>.
/// Legacy shapes — a bare string value, or a secret with a plaintext <c>value</c> — are still read,
/// and a legacy plaintext secret is encrypted the next time the environment is saved.
/// </summary>
public static class EnvironmentVariableSerialization
{
    public const string SecretType = "secret";
    public const string TextType = "text";
    public const string VaultType = "vault";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static List<StoredEnvironmentVariable> ParseStored(string? variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var list = new List<StoredEnvironmentVariable>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        list.Add(new StoredEnvironmentVariable(prop.Name, TextType, prop.Value.GetString(), null));
                        break;
                    case JsonValueKind.Object:
                        var node = prop.Value;
                        var type = NormalizeType(ReadString(node, "type"));
                        list.Add(new StoredEnvironmentVariable(prop.Name, type, ReadString(node, "value"), ReadSecret(node)));
                        break;
                    default:
                        list.Add(new StoredEnvironmentVariable(prop.Name, TextType, null, null));
                        break;
                }
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Encrypts any secret still held as plaintext, then serializes.</summary>
    public static string? SerializeStored(IEnumerable<StoredEnvironmentVariable> variables, ISecretProtector protector)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            if (string.IsNullOrEmpty(variable.Key))
            {
                continue;
            }

            if (!variable.IsSecret)
            {
                dict[variable.Key] = new { value = variable.Value, type = variable.Type };
                continue;
            }

            var secret = variable.Secret
                ?? (string.IsNullOrEmpty(variable.Value) ? null : protector.Protect(variable.Value));

            dict[variable.Key] = secret is null
                ? new { type = SecretType }
                : new
                {
                    type = SecretType,
                    enc = Convert.ToBase64String(secret.Ciphertext),
                    nonce = Convert.ToBase64String(secret.Nonce),
                    keyId = secret.KeyId,
                };
        }

        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOptions);
    }

    /// <summary>What the API returns: secret values are never sent back, only whether one is set.</summary>
    public static List<EnvironmentVariableDto> Parse(string? variablesJson) =>
        ParseStored(variablesJson)
            .Select(v => new EnvironmentVariableDto
            {
                Key = v.Key,
                Type = v.Type,
                Value = v.IsSecret ? null : v.Value,
                HasValue = v.HasValue,
            })
            .ToList();

    /// <summary>
    /// Flattens variables into a key → plaintext map for {{token}} binding at run time, decrypting
    /// stored secrets and fetching vault references. A vault reference that cannot be read binds to
    /// null and is reported through <paramref name="onUnresolved"/>.
    /// </summary>
    public static async Task<Dictionary<string, string?>> ResolveValuesAsync(
        string? variablesJson,
        ISecretProtector protector,
        ISecretStore? secretStore,
        Guid tenantId,
        ISet<string>? secretKeys = null,
        Action<string, string>? onUnresolved = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in ParseStored(variablesJson))
        {
            if (variable.IsSecret || variable.IsVaultReference)
            {
                secretKeys?.Add(variable.Key);
            }

            if (!variable.IsVaultReference)
            {
                result[variable.Key] = variable.Secret is not null ? protector.Unprotect(variable.Secret) : variable.Value;
                continue;
            }

            result[variable.Key] = null;
            if (string.IsNullOrWhiteSpace(variable.Value))
            {
                continue;
            }

            if (secretStore is not { IsConfigured: true })
            {
                onUnresolved?.Invoke(variable.Key, "no secret store is configured");
                continue;
            }

            try
            {
                result[variable.Key] = await secretStore.GetSecretAsync(tenantId, variable.Value, cancellationToken);
            }
            catch (SecretStoreException ex)
            {
                onUnresolved?.Invoke(variable.Key, ex.Message);
            }
        }
        return result;
    }

    public static string NormalizeType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        SecretType => SecretType,
        VaultType => VaultType,
        _ => TextType,
    };

    private static string? ReadString(JsonElement node, string name) =>
        node.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static ProtectedSecret? ReadSecret(JsonElement node)
    {
        var enc = ReadString(node, "enc");
        var nonce = ReadString(node, "nonce");
        var keyId = ReadString(node, "keyId");
        if (string.IsNullOrEmpty(enc) || string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(keyId))
        {
            return null;
        }

        try
        {
            return new ProtectedSecret(Convert.FromBase64String(enc), Convert.FromBase64String(nonce), keyId);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
