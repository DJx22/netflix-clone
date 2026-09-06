namespace Catalog.Api;

/// <summary>
/// Named route constants for the Catalog service.
/// Using constants rather than inline string literals satisfies §16 and keeps
/// the controllers in sync with openapi.yaml without string duplication.
/// </summary>
internal static class Routes
{
    internal const string TitlesBase  = "api/v1/titles";
    internal const string GenresBase  = "api/v1/genres";

    /// <summary>
    /// Suffix for the single-title endpoints: GET, PUT, DELETE /api/v1/titles/{titleId}.
    /// </summary>
    internal const string TitleById   = "{titleId}";

    /// <summary>
    /// Readiness probe — at the service root, not under api/v1 (openapi.yaml).
    /// </summary>
    internal const string Health      = "/health";
}
