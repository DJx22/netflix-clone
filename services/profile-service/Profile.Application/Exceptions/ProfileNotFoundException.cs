using Profile.Domain.Exceptions;

namespace Profile.Application.Exceptions;

/// <summary>
/// Raised when a profileId from the route has no matching row in ProfileDb.
/// Maps to HTTP 404 at the API boundary (openapi.yaml lines 98-103, 135-139, 158-163).
/// </summary>
public sealed class ProfileNotFoundException : DomainException
{
    public ProfileNotFoundException(Guid profileId)
        : base($"No profile found for ID '{profileId}'.")
    {
    }
}
