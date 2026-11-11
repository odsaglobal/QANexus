using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Ai;

/// <summary>
/// <see cref="ILlmClient"/> targeting an OpenAI-compatible chat-completions endpoint (Azure OpenAI /
/// LiteLLM). When the endpoint is unreachable or returns an error the call fails loudly — there is no
/// silent fallback — so callers can surface genuine AI failures instead of persisting sample data.
/// </summary>
public sealed class LlmClient : ILlmClient
{
    private const string HttpClientName = "llm";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 512,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmOptions _options;
    private readonly ILogger<LlmClient> _logger;

    // Parameters a given endpoint+model has rejected, remembered for the process lifetime so only the
    // first call pays for the 400. Keyed by URL and model because one gateway can front many models.
    private static readonly ConcurrentDictionary<string, ModelQuirks> Quirks = new(StringComparer.OrdinalIgnoreCase);

    private sealed class ModelQuirks
    {
        public volatile bool NoTemperature;
        public volatile bool NoResponseFormat;
    }

    public LlmClient(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmOptions> options,
        ILogger<LlmClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsLive => _options.IsConfigured;

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        bool jsonMode = true,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new LlmUnavailableException(
                "The LLM is not configured. Set Llm:Endpoint (or Llm:BaseUrl) and Llm:ApiKey to enable AI analysis.");
        }

        return await CallEndpointAsync(systemPrompt, userPrompt, jsonMode, cancellationToken);
    }

    private async Task<string> CallEndpointAsync(
        string systemPrompt,
        string userPrompt,
        bool jsonMode,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var quirks = Quirks.GetOrAdd($"{_options.ChatCompletionsUrl}|{_options.Model}", _ => new ModelQuirks());

        // Not every OpenAI-compatible model accepts every parameter: reasoning models (o-series, GPT-5,
        // DeepSeek-R1) reject a non-default temperature, and older/local servers (llama.cpp, some LM
        // Studio/Ollama builds) reject response_format. A 400 naming one of these drops it and retries;
        // the prompts already demand JSON and JsonExtraction tolerates the rest.
        var adaptations = 0;

        var maxAttempts = Math.Max(1, _options.MaxRetries);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var includeResponseFormat = jsonMode && !_options.IsAnthropic && !quirks.NoResponseFormat;
            object request = _options.IsAnthropic
                ? BuildAnthropicRequest(systemPrompt, userPrompt, jsonMode)
                : new ChatCompletionRequest
                {
                    Model = _options.Model,
                    Temperature = quirks.NoTemperature ? null : _options.Temperature,
                    ResponseFormat = includeResponseFormat ? new ResponseFormat { Type = "json_object" } : null,
                    Messages =
                    [
                        new ChatMessage { Role = "system", Content = systemPrompt },
                        new ChatMessage { Role = "user", Content = userPrompt },
                    ],
                };

            try
            {
                using var response = await client.PostAsJsonAsync(_options.ChatCompletionsUrl, request, Json, cancellationToken);
                var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var detail = string.IsNullOrWhiteSpace(rawResponse)
                        ? "(empty response body)"
                        : rawResponse.Length > 500 ? rawResponse[..500] : rawResponse;

                    // Transient: rate-limited (429) or upstream/server errors (5xx) are worth retrying with
                    // backoff; anything else (400/401/403/404 etc.) is a real problem that won't fix itself.
                    var isTransient = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                    if (isTransient && attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "LLM call to {Endpoint} failed with {StatusCode} (attempt {Attempt}/{Max}); retrying: {Detail}",
                            _options.ChatCompletionsUrl, (int)response.StatusCode, attempt, maxAttempts, detail);
                        await DelayBeforeRetryAsync(attempt, response.Headers.RetryAfter?.Delta, cancellationToken);
                        continue;
                    }

                    if ((int)response.StatusCode == 400 && !_options.IsAnthropic && adaptations < 2)
                    {
                        var dropTemperature = !quirks.NoTemperature
                            && detail.Contains("temperature", StringComparison.OrdinalIgnoreCase);
                        var dropResponseFormat = includeResponseFormat
                            && (detail.Contains("response_format", StringComparison.OrdinalIgnoreCase)
                                || detail.Contains("json_object", StringComparison.OrdinalIgnoreCase));

                        if (dropTemperature || dropResponseFormat)
                        {
                            quirks.NoTemperature |= dropTemperature;
                            quirks.NoResponseFormat |= dropResponseFormat;
                            adaptations++;
                            maxAttempts++; // the adaptation gets its own attempt regardless of MaxRetries
                            _logger.LogWarning(
                                "LLM {Model} at {Endpoint} rejected {Params}; retrying without it and remembering this for later calls.",
                                _options.Model,
                                _options.ChatCompletionsUrl,
                                string.Join(" and ", new[] { dropTemperature ? "temperature" : null, dropResponseFormat ? "response_format" : null }.Where(p => p is not null)));
                            continue;
                        }
                    }

                    _logger.LogError(
                        "LLM call to {Endpoint} failed with {StatusCode}: {Detail}",
                        _options.ChatCompletionsUrl, (int)response.StatusCode, detail);

                    // Prefer the provider's own wording ("You have reached your specified API usage
                    // limits…") over the raw JSON envelope: this message is shown to testers, who need to
                    // know the AI is unavailable and why, not how the payload was shaped.
                    var providerMessage = ExtractProviderErrorMessage(rawResponse);
                    throw new LlmUnavailableException(
                        string.IsNullOrWhiteSpace(providerMessage)
                            ? $"The AI provider returned {(int)response.StatusCode} ({response.StatusCode}): {detail}"
                            : $"{providerMessage} (HTTP {(int)response.StatusCode} from the AI provider)",
                        (int)response.StatusCode);
                }

                var content = ExtractContent(rawResponse);
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("The LLM returned an empty completion.");
                }

                // The assistant turn was prefilled with "{" to force JSON, and Anthropic echoes only what it
                // generated AFTER the prefill — so put the opening brace back to make the payload parseable.
                if (_options.IsAnthropic && jsonMode)
                {
                    var trimmed = content.TrimStart();
                    if (!trimmed.StartsWith('{'))
                    {
                        content = "{" + content;
                    }
                }

                return content;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientException(ex))
            {
                _logger.LogWarning(ex,
                    "LLM call to {Endpoint} failed transiently (attempt {Attempt}/{Max}); retrying.",
                    _options.ChatCompletionsUrl, attempt, maxAttempts);
                await DelayBeforeRetryAsync(attempt, null, cancellationToken);
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                // Retries are spent and the endpoint is still unreachable. That is an infrastructure
                // outage, so it must reach callers as one rather than as an opaque HttpRequestException.
                _logger.LogError(ex,
                    "LLM call to {Endpoint} failed after {Max} attempt(s).",
                    _options.ChatCompletionsUrl, maxAttempts);
                throw new LlmUnavailableException(
                    $"The AI provider at {_options.ChatCompletionsUrl} is unreachable after {maxAttempts} attempt(s): {ex.Message}",
                    innerException: ex);
            }
        }

        // Unreachable in practice (the loop always returns or throws), but keeps the compiler happy.
        throw new InvalidOperationException("The LLM call did not complete.");
    }

    /// <summary>
    /// A network-level failure (connection refused/reset, DNS, or a client-side timeout) is transient and
    /// worth retrying. A caller-initiated cancellation (the caller's own token, not our timeout) must NOT
    /// be retried — it should propagate immediately as <see cref="OperationCanceledException"/>.
    /// </summary>
    private static bool IsTransientException(Exception ex) =>
        ex is HttpRequestException or TimeoutException
        || (ex is TaskCanceledException && ex.InnerException is TimeoutException);

    /// <summary>
    /// Pulls the human-readable reason out of an error body. OpenAI-compatible and Anthropic endpoints
    /// both nest it under <c>error.message</c>; some gateways return <c>error</c> as a bare string.
    /// </summary>
    private static string ExtractProviderErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return string.Empty;
            }

            return error.ValueKind switch
            {
                JsonValueKind.String => error.GetString() ?? string.Empty,
                JsonValueKind.Object when error.TryGetProperty("message", out var message) =>
                    message.GetString() ?? string.Empty,
                _ => string.Empty,
            };
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>Exponential backoff with jitter, capped at 10s, honouring a server-supplied Retry-After.</summary>
    private static async Task DelayBeforeRetryAsync(int attempt, TimeSpan? retryAfter, CancellationToken ct)
    {
        var backoff = retryAfter ?? TimeSpan.FromMilliseconds(Math.Min(10_000, 500 * Math.Pow(2, attempt - 1)));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
        await Task.Delay(backoff + jitter, ct);
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; init; }

        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; init; }
    }

    /// <summary>
    /// Anthropic's Messages API differs from OpenAI's in three ways that matter here: the system prompt is
    /// a top-level field rather than a message, <c>max_tokens</c> is mandatory, and there is no
    /// <c>response_format</c>. JSON mode is therefore achieved by PREFILLING the assistant turn with an
    /// opening brace — the model can then only continue a JSON object, which is more reliable than asking
    /// it politely and having to strip markdown fences.
    /// </summary>
    private AnthropicRequest BuildAnthropicRequest(string systemPrompt, string userPrompt, bool jsonMode)
    {
        var messages = new List<AnthropicMessage>
        {
            new() { Role = "user", Content = userPrompt },
        };

        if (jsonMode)
        {
            messages.Add(new AnthropicMessage { Role = "assistant", Content = "{" });
        }

        return new AnthropicRequest
        {
            Model = _options.Model,
            MaxTokens = Math.Max(1, _options.MaxTokens),
            Temperature = _options.Temperature,
            System = jsonMode
                ? systemPrompt + "\n\nRespond with a single valid JSON object and nothing else — no prose, no markdown fences."
                : systemPrompt,
            Messages = messages,
        };
    }

    private sealed class AnthropicRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("max_tokens")]
        public required int MaxTokens { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("system")]
        public string? System { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<AnthropicMessage> Messages { get; init; }
    }

    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class ResponseFormat
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }

        [JsonPropertyName("result")]
        public ResultWrapper? Result { get; init; }
    }

    private sealed class ResultWrapper
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; init; }
    }

    private static string? ExtractContent(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        try
        {
            var typed = JsonSerializer.Deserialize<ChatCompletionResponse>(rawResponse, Json);
            var typedContent = typed?.Choices?.FirstOrDefault()?.Message?.Content
                ?? typed?.Result?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(typedContent))
            {
                return typedContent;
            }
        }
        catch (JsonException)
        {
            // Fall through to tolerant DOM parsing below.
        }

        using var doc = JsonDocument.Parse(rawResponse, new JsonDocumentOptions { MaxDepth = 512 });
        var root = doc.RootElement;

        if (TryExtractContentFromElement(root, out var content))
        {
            return content;
        }

        if (TryExtractAnthropicContent(root, out content))
        {
            return content;
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("result", out var result)
            && TryExtractContentFromElement(result, out content))
        {
            return content;
        }

        return null;
    }

    /// <summary>
    /// Reads Anthropic's Messages API shape: <c>{ "content": [ { "type": "text", "text": "…" } ] }</c>.
    /// Text blocks are concatenated because a response can be split across several of them.
    /// </summary>
    private static bool TryExtractAnthropicContent(JsonElement root, out string? content)
    {
        content = null;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("content", out var blocks)
            || blocks.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var block in blocks.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                sb.Append(text.GetString());
            }
        }

        if (sb.Length == 0)
        {
            return false;
        }

        content = sb.ToString();
        return true;
    }

    private static bool TryExtractContentFromElement(JsonElement element, out string? content)
    {
        content = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return false;
        }

        var first = choices[0];
        if (first.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!first.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        content = ReadMessageText(message, "content");

        // Reasoning models served through some gateways (DeepSeek-R1, Qwen QwQ on vLLM) put everything in
        // a separate reasoning field and leave content empty; the JSON is usually at its end.
        if (string.IsNullOrWhiteSpace(content))
        {
            content = ReadMessageText(message, "reasoning_content") ?? ReadMessageText(message, "reasoning");
        }

        return !string.IsNullOrWhiteSpace(content);
    }

    /// <summary>Reads a message field that may be a string or an array of <c>{type:"text",text}</c> parts.</summary>
    private static string? ReadMessageText(JsonElement message, string property)
    {
        if (!message.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var part in value.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                sb.Append(part.GetString());
            }
            else if (part.ValueKind == JsonValueKind.Object
                && part.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                sb.Append(text.GetString());
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}
