namespace ATIP.Application.Common.Exceptions;

/// <summary>
/// The AI provider itself could not serve the request — no endpoint/key is configured, the account is
/// out of quota, the credentials were rejected, or the endpoint is unreachable.
/// </summary>
/// <remarks>
/// Deliberately distinct from an ordinary failure for two reasons. Retrying is pointless, so callers
/// must stop rather than burn their whole turn budget on calls that cannot succeed. More importantly it
/// says NOTHING about the application under test: a step that never ran must be reported as not-run,
/// never as a test failure, or testers will hunt for a defect that does not exist.
/// </remarks>
public sealed class LlmUnavailableException : Exception
{
    public LlmUnavailableException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status the provider returned, when the failure came from a response.</summary>
    public int? StatusCode { get; }
}
