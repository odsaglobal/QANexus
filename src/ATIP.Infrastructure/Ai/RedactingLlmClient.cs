using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;

namespace ATIP.Infrastructure.Ai;

/// <summary>Strips secret values from every prompt before it reaches the AI provider.</summary>
internal sealed class RedactingLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;
    private readonly SecretRedactor _redactor;

    public RedactingLlmClient(ILlmClient inner, SecretRedactor redactor)
    {
        _inner = inner;
        _redactor = redactor;
    }

    public bool IsLive => _inner.IsLive;

    public Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        bool jsonMode = true,
        CancellationToken cancellationToken = default) =>
        _inner.CompleteAsync(_redactor.Redact(systemPrompt), _redactor.Redact(userPrompt), jsonMode, cancellationToken);
}
