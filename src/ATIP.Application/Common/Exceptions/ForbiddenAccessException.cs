namespace ATIP.Application.Common.Exceptions;

/// <summary>Caller is authenticated but lacks permission for the operation. Maps to HTTP 403.</summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}
