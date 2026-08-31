namespace Identity.Domain.Exceptions;

/// <summary>
/// Base class for all domain rule violations in the Identity bounded context.
/// Application layer catches these and maps them to typed HTTP responses —
/// they are expected failure paths, not infrastructure faults.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
