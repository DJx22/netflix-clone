namespace Identity.Domain.Exceptions;

/// <summary>
/// Raised when a refresh token is not found, already revoked, or (for rotation only) expired.
/// Maps to HTTP 401 at the API boundary (openapi.yaml lines 110-115, 137-142).
/// </summary>
public sealed class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException(string message) : base(message)
    {
    }
}
