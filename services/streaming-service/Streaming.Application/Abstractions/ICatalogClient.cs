namespace Streaming.Application.Abstractions;

/// <summary>
/// Narrow application-facing dependency on the Catalog service.
/// Used only in the post-miss diagnostic flow: when Streaming has no
/// <see cref="Streaming.Domain.Entities.MediaAsset"/> for a <c>titleId</c>, it calls
/// this client to check whether Catalog knows the title — for logging/observability
/// only. The result never changes what Streaming returns (ADR 0006 §3).
/// </summary>
/// <remarks>
/// Implemented in <c>Streaming.Infrastructure</c>. The implementation must handle
/// HTTP errors and timeouts and map them to
/// <see cref="CatalogTitleCheckResult.DependencyFailure()"/> — never let them
/// propagate as unhandled exceptions.
/// </remarks>
public interface ICatalogClient
{
    /// <summary>
    /// Checks whether <paramref name="titleId"/> exists in the Catalog service.
    /// </summary>
    /// <param name="titleId">The title identifier to look up.</param>
    /// <param name="cancellationToken">Propagated to the outbound HTTP call.</param>
    /// <returns>
    /// A <see cref="CatalogTitleCheckResult"/> with one of three statuses:
    /// <see cref="CatalogTitleCheckStatus.Found"/>,
    /// <see cref="CatalogTitleCheckStatus.NotFound"/>, or
    /// <see cref="CatalogTitleCheckStatus.DependencyFailure"/>.
    /// </returns>
    Task<CatalogTitleCheckResult> CheckTitleExistsAsync(
        string titleId,
        CancellationToken cancellationToken = default);
}
