namespace Streaming.Application.Exceptions;

/// <summary>
/// Thrown when a repository query returns no saved <c>PlaybackPosition</c> for the
/// requested (<c>titleId</c>, <c>profileId</c>) pair. Maps to HTTP 404 at the API boundary.
/// </summary>
/// <remarks>
/// <para>
/// The primary not-found path in <c>GetPositionAsync</c> uses
/// <see cref="Results.GetPositionResult.NotFound"/> (a typed result) rather than
/// throwing this exception, to keep the controller free of exception-flow coupling
/// for expected API paths. This exception is available for use cases where a saved
/// position is a hard pre-condition before proceeding.
/// </para>
/// </remarks>
public sealed class PlaybackPositionNotFoundException : Exception
{
    /// <param name="titleId">The title identifier.</param>
    /// <param name="profileId">The profile identifier.</param>
    public PlaybackPositionNotFoundException(string titleId, Guid profileId)
        : base($"No saved playback position found for title '{titleId}' and profile '{profileId}'.")
    {
    }
}
