using Streaming.Application.Abstractions;

namespace Streaming.Infrastructure.ExternalClients;

/// <summary>
/// HTTP implementation of <see cref="ICatalogClient"/>.
/// Used only in the post-miss diagnostic flow: when Streaming has no
/// <c>MediaAsset</c> for a <c>titleId</c>, this client checks whether Catalog
/// knows the title — for logging/observability only. The result never changes
/// what Streaming returns to its caller (ADR 0006 §3).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="HttpClient"/> is injected by the DI container (registered via
/// <c>AddHttpClient&lt;CatalogHttpClient&gt;</c>) with <c>BaseAddress</c> pre-configured
/// from <c>CatalogClientOptions.BaseUrl</c>. No manual <c>new HttpClient()</c> anywhere.
/// </para>
/// <para>
/// Catalog's <c>TitleDetailResponse</c> is <b>never deserialised</b>. Only the HTTP
/// status code is read — no Catalog metadata enters Streaming's type system.
/// </para>
/// <para>
/// Dependency failures (network errors, timeouts, unexpected HTTP status codes) are
/// mapped to <see cref="CatalogTitleCheckStatus.DependencyFailure"/> and must never
/// be re-thrown. Streaming logs the outcome and returns its own 404 regardless.
/// </para>
/// </remarks>
public sealed class CatalogHttpClient : ICatalogClient
{
    private readonly HttpClient _httpClient;

    /// <summary>Initialises a new <see cref="CatalogHttpClient"/>.</summary>
    public CatalogHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sends <c>GET /api/v1/titles/{titleId}</c> against the Catalog service base URL.
    /// Status mapping:
    /// <list type="bullet">
    ///   <item><c>200 OK</c> → <see cref="CatalogTitleCheckStatus.Found"/></item>
    ///   <item><c>404 Not Found</c> → <see cref="CatalogTitleCheckStatus.NotFound"/></item>
    ///   <item>Any other status code → <see cref="CatalogTitleCheckStatus.DependencyFailure"/></item>
    ///   <item><see cref="HttpRequestException"/> or <see cref="TaskCanceledException"/> →
    ///         <see cref="CatalogTitleCheckStatus.DependencyFailure"/></item>
    /// </list>
    /// </remarks>
    public async Task<CatalogTitleCheckResult> CheckTitleExistsAsync(
        string titleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Only the status code matters — do not read or buffer the response body.
            using var response = await _httpClient
                .GetAsync(
                    $"api/v1/titles/{Uri.EscapeDataString(titleId)}",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return CatalogTitleCheckResult.Found();
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return CatalogTitleCheckResult.NotFound();
            }

            // Any other status (5xx, 401, 503, …) is a dependency failure, not
            // a meaningful "title not found" signal. Distinguishing them avoids
            // masking a Catalog outage as "title does not exist".
            return CatalogTitleCheckResult.DependencyFailure();
        }
        catch (HttpRequestException)
        {
            // Network-level failure (DNS, TCP reset, etc.)
            return CatalogTitleCheckResult.DependencyFailure();
        }
        catch (TaskCanceledException)
        {
            // Request timeout or caller-initiated cancellation — treat both as failure
            // so Streaming doesn't claim the title is absent when Catalog was merely slow.
            return CatalogTitleCheckResult.DependencyFailure();
        }
    }
}
