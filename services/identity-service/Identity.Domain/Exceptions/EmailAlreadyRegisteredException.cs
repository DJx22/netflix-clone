namespace Identity.Domain.Exceptions;

/// <summary>
/// Raised when a registration attempt uses an email address that is already in use.
/// Maps to HTTP 409 Conflict at the API boundary (openapi.yaml lines 50-55).
/// The check is a repository concern (requires a DB lookup), but the exception is a
/// domain concept — the rule "one account per email" belongs to the domain.
/// </summary>
public sealed class EmailAlreadyRegisteredException : DomainException
{
    public EmailAlreadyRegisteredException(string email)
        : base($"The email address '{email}' is already registered.")
    {
    }
}
