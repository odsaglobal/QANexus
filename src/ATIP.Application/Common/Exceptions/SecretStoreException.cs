namespace ATIP.Application.Common.Exceptions;

/// <summary>
/// A secret could not be read from the external store. The message names the reference and the
/// reason, never the value.
/// </summary>
public sealed class SecretStoreException : Exception
{
    public SecretStoreException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
