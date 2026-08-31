using Identity.Domain.Exceptions;

namespace Identity.Application.Exceptions;

/// <summary>
/// Raised when an email/password pair fails authentication.
/// Message is deliberately generic to prevent account enumeration
/// (openapi.yaml line 80 returns a single 401 without distinguishing the cause).
/// </summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }
}
