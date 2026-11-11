using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Security;

/// <summary>
/// Reads secrets from a HashiCorp Vault KV v2 engine over its HTTP API, authenticating with AppRole
/// (or a static token). Tokens are renewed by logging in again shortly before they expire.
/// </summary>
public sealed class HashiCorpVaultSecretStore : ISecretStore, IDisposable
{
    public const string HttpClientName = "vault";

    // Renew before the lease actually lapses so an in-flight read never races the expiry.
    private static readonly TimeSpan TokenExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpFactory;
    private readonly VaultOptions _options;
    private readonly ILogger<HashiCorpVaultSecretStore> _logger;
    private readonly SemaphoreSlim _loginGate = new(1, 1);
    private readonly ConcurrentDictionary<string, (IReadOnlyDictionary<string, string> Data, DateTimeOffset Expires)> _cache = new();

    private string? _token;
    private DateTimeOffset _tokenExpires = DateTimeOffset.MinValue;

    public HashiCorpVaultSecretStore(
        IHttpClientFactory httpFactory,
        IOptions<VaultOptions> options,
        ILogger<HashiCorpVaultSecretStore> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Provider => "HashiCorp Vault";

    public bool IsConfigured => _options.IsConfigured;

    public async Task<string> GetSecretAsync(Guid tenantId, string reference, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new SecretStoreException("No secret store is configured. Set the 'Vault' section (Enabled, Address and credentials).");
        }

        if (!SecretReference.TryParse(reference, out var parsed))
        {
            throw new SecretStoreException($"'{reference}' is not a valid secret reference. Use path/to/secret#field.");
        }

        var fullPath = BuildPath(tenantId, parsed.Path);
        var data = await ReadAsync(fullPath, cancellationToken);

        return data.TryGetValue(parsed.Field, out var value)
            ? value
            : throw new SecretStoreException($"Secret '{parsed.Path}' has no field '{parsed.Field}'.");
    }

    public async Task<SecretStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new SecretStoreStatus(Provider, false, false, false, "Vault integration is disabled.");
        }

        if (!IsConfigured)
        {
            return new SecretStoreStatus(Provider, false, false, false, "Vault is enabled but the address or credentials are missing.");
        }

        try
        {
            using var health = await Client().GetAsync("v1/sys/health?standbyok=true&perfstandbyok=true", cancellationToken);
            if (health.StatusCode is HttpStatusCode.ServiceUnavailable)
            {
                return new SecretStoreStatus(Provider, true, true, false, "Vault is sealed.");
            }

            if (!health.IsSuccessStatusCode)
            {
                return new SecretStoreStatus(Provider, true, false, false, $"Vault health check returned {(int)health.StatusCode}.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new SecretStoreStatus(Provider, true, false, false, $"Vault is unreachable at {_options.Address}.");
        }

        try
        {
            await GetTokenAsync(forceLogin: false, cancellationToken);
            return new SecretStoreStatus(Provider, true, true, true, null);
        }
        catch (SecretStoreException ex)
        {
            return new SecretStoreStatus(Provider, true, true, false, ex.Message);
        }
    }

    public void Dispose() => _loginGate.Dispose();

    private string BuildPath(Guid tenantId, string path)
    {
        var prefix = _options.PathPrefix.Replace("{tenantId}", tenantId.ToString(), StringComparison.OrdinalIgnoreCase).Trim('/');
        return string.IsNullOrEmpty(prefix) ? path : $"{prefix}/{path}";
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadAsync(string fullPath, CancellationToken ct)
    {
        if (_cache.TryGetValue(fullPath, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
        {
            return cached.Data;
        }

        var url = $"v1/{_options.MountPath.Trim('/')}/data/{EscapePath(fullPath)}";
        using var response = await SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SecretStoreException($"Secret '{fullPath}' was not found in Vault.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new SecretStoreException($"Vault denied access to '{fullPath}'. Check the policy attached to the Patterns.ai AppRole.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SecretStoreException($"Vault returned {(int)response.StatusCode} reading '{fullPath}'.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var outer)
            || !outer.TryGetProperty("data", out var inner)
            || inner.ValueKind != JsonValueKind.Object)
        {
            throw new SecretStoreException($"Secret '{fullPath}' is deleted or is not a KV v2 secret.");
        }

        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in inner.EnumerateObject())
        {
            data[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText();
        }

        if (_options.CacheSeconds > 0)
        {
            _cache[fullPath] = (data, DateTimeOffset.UtcNow.AddSeconds(_options.CacheSeconds));
        }

        return data;
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        try
        {
            var token = await GetTokenAsync(forceLogin: false, ct);
            var response = await SendWithTokenAsync(build(), token, ct);

            // An AppRole token can be revoked before its lease ends; log in again once and retry.
            if (response.StatusCode == HttpStatusCode.Forbidden && _options.UsesAppRole)
            {
                response.Dispose();
                token = await GetTokenAsync(forceLogin: true, ct);
                response = await SendWithTokenAsync(build(), token, ct);
            }

            return response;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new SecretStoreException($"Vault is unreachable at {_options.Address}.", ex);
        }
    }

    private Task<HttpResponseMessage> SendWithTokenAsync(HttpRequestMessage request, string token, CancellationToken ct)
    {
        request.Headers.Add("X-Vault-Token", token);
        return Client().SendAsync(request, ct);
    }

    private async Task<string> GetTokenAsync(bool forceLogin, CancellationToken ct)
    {
        if (!_options.UsesAppRole)
        {
            return _options.Token!;
        }

        if (!forceLogin && _token is not null && DateTimeOffset.UtcNow < _tokenExpires)
        {
            return _token;
        }

        await _loginGate.WaitAsync(ct);
        try
        {
            if (!forceLogin && _token is not null && DateTimeOffset.UtcNow < _tokenExpires)
            {
                return _token;
            }

            using var response = await Client().PostAsJsonAsync(
                $"v1/auth/{_options.AppRoleMountPath.Trim('/')}/login",
                new { role_id = _options.RoleId, secret_id = _options.SecretId },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vault AppRole login failed with HTTP {Status}.", (int)response.StatusCode);
                throw new SecretStoreException($"Vault AppRole login failed ({(int)response.StatusCode}). Check RoleId/SecretId.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var auth = doc.RootElement.GetProperty("auth");
            _token = auth.GetProperty("client_token").GetString()
                ?? throw new SecretStoreException("Vault AppRole login returned no token.");

            var lease = auth.TryGetProperty("lease_duration", out var l) && l.TryGetInt32(out var seconds) ? seconds : 300;
            _tokenExpires = DateTimeOffset.UtcNow.AddSeconds(lease) - TokenExpiryMargin;
            return _token;
        }
        finally
        {
            _loginGate.Release();
        }
    }

    private HttpClient Client() => _httpFactory.CreateClient(HttpClientName);

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
