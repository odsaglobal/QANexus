namespace ATIP.Infrastructure.Configuration;

/// <summary>
/// Configuration for the LLM adapter, bound from section "Llm" (and the legacy alias "LlmResolver").
/// Supports any OpenAI-compatible chat-completions endpoint (OpenAI, Azure OpenAI, LiteLLM, Groq,
/// OpenRouter, local servers such as LM Studio/Ollama/vLLM) plus Anthropic's native Messages API.
/// When neither an endpoint nor an API key is configured, <see cref="ILlmClient.CompleteAsync"/> throws
/// <c>LlmUnavailableException</c> rather than silently falling back to mock data.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public const string ResolverSectionName = "LlmResolver";

    /// <summary>
    /// Provider name, e.g. "openai", "azure" or "anthropic". Azure endpoints with openai.azure.com and
    /// Anthropic endpoints with api.anthropic.com are detected automatically.
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

    /// <summary>
    /// Max attempts for a single completion when the endpoint returns a transient error (429, 502/503/504,
    /// or a connection failure). 1 = no retry. Uses exponential backoff with jitter between attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    public int MaxSelectors { get; set; } = 5;

    public bool EnableVision { get; set; }

    public bool EnableAriaTree { get; set; } = true;

    /// <summary>
    /// True when a real endpoint is configured. An API key is required for hosted providers (OpenAI,
    /// Azure, Anthropic — inferred from <see cref="Provider"/>/endpoint), but many self-hosted
    /// OpenAI-compatible servers (LM Studio, Ollama, vLLM, text-generation-webui) accept requests with no
    /// key at all, so an endpoint alone is sufficient for those.
    /// </summary>
    public bool IsConfigured => IsAnthropic
        ? !string.IsNullOrWhiteSpace(ApiKey)
        : !string.IsNullOrWhiteSpace(Endpoint) || !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>True when the configured endpoint targets Azure OpenAI.</summary>
    public bool IsAzure => string.Equals(Provider, "azure", StringComparison.OrdinalIgnoreCase)
        || RawEndpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the configured endpoint targets Anthropic's native Messages API. Anthropic is NOT
    /// OpenAI-compatible: it uses a different URL, auth header, request body and response shape.
    /// </summary>
    public bool IsAnthropic => string.Equals(Provider, "anthropic", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "claude", StringComparison.OrdinalIgnoreCase)
        || RawEndpoint.Contains("api.anthropic.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Version header required on every Anthropic Messages API call.
    /// </summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Upper bound on tokens the model may generate. Anthropic REQUIRES this on every request; the
    /// exploration agent's JSON decisions are small, but scenario generation returns long documents.
    /// </summary>
    public int MaxTokens { get; set; } = 8192;

    private string RawEndpoint => !string.IsNullOrWhiteSpace(Endpoint) ? Endpoint.Trim() : BaseUrl.Trim();

    /// <summary>
    /// Absolute chat-completions URL to POST to. For Azure this preserves the deployment path and the
    /// required <c>api-version</c> query string; for Anthropic it resolves to the Messages API; for
    /// OpenAI-compatible servers it appends <c>/v1/chat/completions</c> when only a host is supplied.
    /// </summary>
    public string ChatCompletionsUrl
    {
        get
        {
            var endpoint = RawEndpoint;

            if (IsAnthropic)
            {
                // Anthropic needs no per-deployment URL, so the endpoint is normally omitted. Ignore one that
                // points elsewhere: switching provider by overriding only Provider would otherwise graft the
                // Messages path onto a leftover Azure deployment URL and fail with a confusing 404.
                var host = endpoint.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase)
                    ? endpoint.TrimEnd('/')
                    : "https://api.anthropic.com";
                return host.Contains("/messages", StringComparison.OrdinalIgnoreCase)
                    ? host
                    : host.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? host + "/messages" : host + "/v1/messages";
            }

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
