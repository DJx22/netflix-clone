namespace Streaming.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the <c>CatalogClient</c> configuration section.
/// Resolved via <c>IOptions&lt;CatalogClientOptions&gt;</c> at DI registration time —
/// never scattered as raw <c>IConfiguration</c> reads (§6).
/// </summary>
public sealed class CatalogClientOptions
{
    /// <summary>
    /// Gets the base URL of the Catalog service (e.g. <c>http://catalog:8080</c>).
    /// The <see cref="System.Net.Http.HttpClient"/> is configured with this as its
    /// <c>BaseAddress</c> in <c>DependencyInjection.cs</c>.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;
}
