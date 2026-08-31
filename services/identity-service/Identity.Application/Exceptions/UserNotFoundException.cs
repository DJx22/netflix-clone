using Identity.Domain.Exceptions;

namespace Identity.Application.Exceptions;

/// <summary>
/// Raised when a UserId extracted from a valid JWT has no matching row in IdentityDb.
/// Should be vanishingly rare in production (deleted account, data integrity issue).
/// Maps to HTTP 401 at the API boundary — leaking "user not found" would be an
/// information disclosure for callers probing with fabricated tokens.
/// </summary>
public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(Guid userId)
        : base($"No user found for ID '{userId}'.")
    {
    }
}
