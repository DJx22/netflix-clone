namespace Streaming.Api;

/// <summary>
/// Named route constants for the Streaming service.
/// Constants instead of inline string literals (§16) keep
/// <see cref="Controllers.PlaybackController"/> and
/// <see cref="Controllers.HealthController"/> in sync with openapi.yaml.
/// </summary>
internal static class Routes
{
    /// <summary>Base route shared by all three playback endpoints.</summary>
    internal const string PlaybackBase = "api/v1/playback/{titleId}";

    /// <summary>Suffix for the media-lookup endpoint.</summary>
    internal const string Media = "media";

    /// <summary>Suffix for the playback-position GET and PUT endpoints.</summary>
    internal const string Position = "position";

    /// <summary>
    /// Readiness probe — at the service root, not under api/v1 (openapi.yaml).
    /// </summary>
    internal const string Health = "/health";
}
