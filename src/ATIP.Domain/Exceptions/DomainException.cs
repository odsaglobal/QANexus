namespace ATIP.Domain.Exceptions;

/// <summary>
/// Base class for all domain rule violations. Distinguishing these from generic
/// exceptions lets the API layer translate them into 4xx responses rather than 500s.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when a domain invariant would be violated by an operation.</summary>
public sealed class DomainRuleException : DomainException
{
    public DomainRuleException(string message) : base(message)
    {
    }
}
