namespace Profile.Domain.Exceptions;

/// <summary>
/// Raised when a caller attempts to access or mutate a profile
/// that belongs to a different account than the one in the JWT.
/// Maps to HTTP 403 at the API boundary (openapi.yaml lines 92-97, 128-133, 152-157).
/// </summary>
public sealed class ProfileNotOwnedException : DomainException
{
    public ProfileNotOwnedException()
        : base("The profile belongs to a different account.")
    {
    }
}
