using Streaming.Application.DTOs;

namespace Streaming.Application.Results;

/// <summary>
/// Typed result for the <c>GetPosition</c> use case.
/// Replaces exceptions for the expected not-found path so the controller can
/// switch on the outcome without relying on the global exception handler for
/// a normal API flow (OpenAPI 200 vs 404).
/// </summary>
public abstract class GetPositionResult
{
    private GetPositionResult() { }

    /// <summary>A saved position was found; the response is ready to return.</summary>
    public sealed class Found : GetPositionResult
    {
        /// <summary>The mapped playback-position response.</summary>
        public PlaybackPositionResponse Response { get; }

        internal Found(PlaybackPositionResponse response)
        {
            Response = response;
        }
    }

    /// <summary>
    /// No position has been recorded yet for the (<c>titleId</c>, <c>profileId</c>) pair.
    /// Maps to HTTP 404.
    /// </summary>
    public sealed class NotFound : GetPositionResult
    {
        internal NotFound() { }
    }

    /// <summary>Creates a <see cref="Found"/> result wrapping <paramref name="response"/>.</summary>
    public static GetPositionResult Success(PlaybackPositionResponse response) => new Found(response);

    /// <summary>Creates a <see cref="NotFound"/> result.</summary>
    public static GetPositionResult Miss() => new NotFound();
}
