namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Provider-agnostic chat-completion abstraction. The infrastructure implementation targets a
/// LiteLLM (OpenAI-compatible) endpoint, and falls back to a deterministic local generator when
/// no endpoint/key is configured so the platform runs offline for development and testing.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a system+user prompt and returns the raw assistant message content. Implementations
    /// should request and expect a JSON response when <paramref name="jsonMode"/> is true.
    /// </summary>
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        bool jsonMode = true,
        CancellationToken cancellationToken = default);

    /// <summary>True when a real LLM endpoint is configured; false when using the local mock.</summary>
    bool IsLive { get; }
}
