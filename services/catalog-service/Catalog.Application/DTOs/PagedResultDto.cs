namespace Catalog.Application.DTOs;

/// <summary>
/// Generic paged result envelope, matching the OpenAPI spec's
/// <c>PagedTitleSummaryResponse</c> shape.
/// </summary>
/// <typeparam name="T">The item type in the page.</typeparam>
/// <remarks>
/// Kept generic so Infrastructure can reuse the same envelope shape if other
/// paged read paths are added later without duplicating the wrapper type.
/// </remarks>
public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
