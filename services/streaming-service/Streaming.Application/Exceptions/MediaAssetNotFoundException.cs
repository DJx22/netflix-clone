namespace Streaming.Application.Exceptions;

/// <summary>
/// Thrown when a repository query returns no <c>MediaAsset</c> for the requested
/// <c>titleId</c>. Maps to HTTP 404 at the API boundary.
/// </summary>
/// <remarks>
/// <para>
/// This is an Application-level concern, not a domain rule violation. The domain
/// models what a <c>MediaAsset</c> <em>is</em>; the application layer handles the
/// case where one does not exist for a given <c>titleId</c>.
/// </para>
/// <para>
/// The primary not-found path in <c>GetMediaAsync</c> uses
/// <see cref="Results.GetMediaResult.NotFound"/> (a typed result) rather than
/// throwing this exception, to keep the controller free of exception-flow coupling
/// for expected API paths. This exception is available for use cases where a
/// <c>MediaAsset</c> is a hard pre-condition (e.g. a future write endpoint that
/// requires the asset to exist before proceeding).
/// </para>
/// </remarks>
public sealed class MediaAssetNotFoundException : Exception
{
    /// <param name="titleId">The title for which no media asset was found.</param>
    public MediaAssetNotFoundException(string titleId)
        : base($"No media asset found in StreamingDb for title '{titleId}'.")
    {
    }
}
