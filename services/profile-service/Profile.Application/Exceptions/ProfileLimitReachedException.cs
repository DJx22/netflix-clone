using Profile.Domain.Exceptions;

namespace Profile.Application.Exceptions;

/// <summary>
/// Raised when a caller's account already has the maximum number of profiles.
/// Maps to HTTP 409 at the API boundary (openapi.yaml lines 68-73).
/// The limit (5) is an Application-layer rule — the spec explicitly states
/// this belongs in a validator, not a domain entity or database constraint.
/// </summary>
public sealed class ProfileLimitReachedException : DomainException
{
    public const int MaxProfilesPerAccount = 5;

    public ProfileLimitReachedException()
        : base($"Account already has the maximum number of profiles ({MaxProfilesPerAccount}).")
    {
    }
}
