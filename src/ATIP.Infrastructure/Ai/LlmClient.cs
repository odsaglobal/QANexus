using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            throw new InvalidOperationException(
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

        var request = new ChatCompletionRequest
        {
            Model = _options.Model,
            Temperature = _options.Temperature,
            ResponseFormat = jsonMode ? new ResponseFormat { Type = "json_object" } : null,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt },
            ],
        };

        using var response = await client.PostAsJsonAsync(_options.ChatCompletionsUrl, request, Json, cancellationToken);

        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = string.IsNullOrWhiteSpace(rawResponse)
                ? "(empty response body)"
                : rawResponse.Length > 500 ? rawResponse[..500] : rawResponse;
            _logger.LogError(
                "LLM call to {Endpoint} failed with {StatusCode}: {Detail}",
                _options.ChatCompletionsUrl, (int)response.StatusCode, detail);
            throw new InvalidOperationException(
                $"The LLM endpoint returned {(int)response.StatusCode} ({response.StatusCode}): {detail}");
        }

        var content = ExtractContent(rawResponse);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The LLM returned an empty completion.");
        }

        return content;
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; init; }
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

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("result", out var result)
            && TryExtractContentFromElement(result, out content))
        {
            return content;
        }

        return null;
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

        if (!message.TryGetProperty("content", out var contentElement)
            || contentElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        content = contentElement.GetString();
        return !string.IsNullOrWhiteSpace(content);
    }
}
