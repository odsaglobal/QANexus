using FluentValidation.Results;

namespace ATIP.Application.Common.Exceptions;

/// <summary>
/// Aggregates FluentValidation failures into a field-keyed dictionary. The API layer
/// serializes this into an RFC 7807 ValidationProblemDetails (HTTP 400).
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
