namespace Payment.Domain.Exceptions;

/// <summary>
/// Base class for all domain rule violations in the Payment bounded context.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
