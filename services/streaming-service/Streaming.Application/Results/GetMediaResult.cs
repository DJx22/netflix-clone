using Streaming.Application.DTOs;

namespace Streaming.Application.Results;

/// <summary>
/// Typed result for the <c>GetMedia</c> use case.
/// Replaces exceptions for the expected not-found path so the controller can
/// switch on the outcome without relying on the global exception handler for
/// a normal API flow (OpenAPI 200 vs 404).
/// </summary>
public abstract class GetMediaResult
{
    private GetMediaResult() { }

    /// <summary>A <see cref="MediaAsset"/> was found; the response is ready to return.</summary>
    public sealed class Found : GetMediaResult
    {
        /// <summary>The mapped media response.</summary>
        public MediaResponse Response { get; }

        internal Found(MediaResponse response)
        {
            Response = response;
        }
    }

    /// <summary>
    /// No <see cref="Streaming.Domain.Entities.MediaAsset"/> is registered in
    /// <c>StreamingDb</c> for the requested <c>titleId</c>. Maps to HTTP 404.
    /// The Catalog diagnostic result (if any) is logged but does not change this outcome.
    /// </summary>
    public sealed class NotFound : GetMediaResult
    {
        internal NotFound() { }
    }

    /// <summary>Creates a <see cref="Found"/> result wrapping <paramref name="response"/>.</summary>
    public static GetMediaResult Success(MediaResponse response) => new Found(response);

    /// <summary>Creates a <see cref="NotFound"/> result.</summary>
    public static GetMediaResult Miss() => new NotFound();
}
