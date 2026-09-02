namespace Subscription.Domain.Exceptions;

/// <summary>
/// Base class for all domain rule violations in the Subscription bounded context.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
