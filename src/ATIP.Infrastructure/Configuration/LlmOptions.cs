namespace ATIP.Infrastructure.Configuration;

/// <summary>
/// Configuration for the LLM adapter, bound from section "Llm" (and the legacy alias "LlmResolver").
/// Supports OpenAI-compatible endpoints and Azure OpenAI deployment URLs.
/// When <see cref="BaseUrl"/>/<see cref="Endpoint"/> or <see cref="ApiKey"/> is empty the adapter runs a
/// deterministic offline mock instead.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public const string ResolverSectionName = "LlmResolver";

    /// <summary>
    /// Provider name, e.g. "openai" or "azure". Azure endpoints with openai.azure.com are detected automatically.
    /// </summary>
    public string Provider { get; set; } = "openai";

    /// <summary>Azure OpenAI deployment endpoint or OpenAI-compatible base URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Base URL of the OpenAI-compatible API, e.g. http://localhost:4000/v1 (LiteLLM).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 120;

    public int TimeoutMs
    {
        get => TimeoutSeconds * 1000;
        set => TimeoutSeconds = Math.Max(1, (int)Math.Ceiling(value / 1000d));
    }

    public double Temperature { get; set; } = 0.4;

    public int MaxSelectors { get; set; } = 5;

    public bool EnableVision { get; set; }

    public bool EnableAriaTree { get; set; } = true;

    /// <summary>True when a real endpoint is configured.</summary>
    public bool IsConfigured => (!string.IsNullOrWhiteSpace(Endpoint) || !string.IsNullOrWhiteSpace(BaseUrl))
        && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>True when the configured endpoint targets Azure OpenAI.</summary>
    public bool IsAzure => string.Equals(Provider, "azure", StringComparison.OrdinalIgnoreCase)
        || RawEndpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase);

    private string RawEndpoint => !string.IsNullOrWhiteSpace(Endpoint) ? Endpoint.Trim() : BaseUrl.Trim();

    /// <summary>
    /// Absolute chat-completions URL to POST to. For Azure this preserves the deployment path and the
    /// required <c>api-version</c> query string; for OpenAI-compatible servers it appends
    /// <c>/v1/chat/completions</c> when only a host is supplied.
    /// </summary>
    public string ChatCompletionsUrl
    {
        get
        {
            var endpoint = RawEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return string.Empty;
            }

            // Endpoint already points at chat/completions (typical Azure deployment URL) — use it verbatim
            // so the api-version query string is preserved.
            if (endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }

            var trimmed = endpoint.TrimEnd('/');

            if (IsAzure)
            {
                return trimmed + "/chat/completions";
            }

            // OpenAI-compatible: default host-only URLs to the /v1 base path.
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/"))
            {
                trimmed += "/v1";
            }

            return trimmed + "/chat/completions";
        }
    }
}
