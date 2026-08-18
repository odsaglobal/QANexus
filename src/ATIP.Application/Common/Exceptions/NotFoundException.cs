namespace ATIP.Application.Common.Exceptions;

/// <summary>Requested resource does not exist (or is not visible to the caller). Maps to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.")
    {
    }
}
