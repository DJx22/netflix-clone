namespace Streaming.Application.Abstractions;

/// <summary>
/// Describes the three outcomes of checking whether a <c>titleId</c> exists in the
/// Catalog service. Used only when Streaming has no <c>MediaAsset</c> for the title —
/// the check is diagnostic, not a fallback data source (ADR 0006 §3).
/// </summary>
public enum CatalogTitleCheckStatus
{
    /// <summary>Catalog confirmed the title exists.</summary>
    Found,

    /// <summary>Catalog confirmed the title does not exist.</summary>
    NotFound,

    /// <summary>
    /// The Catalog service was unreachable or returned an unexpected error.
    /// Streaming logs this and still returns 404 — the dependency failure does not
    /// change the Streaming response.
    /// </summary>
    DependencyFailure
}

/// <summary>
/// Typed result returned by <see cref="ICatalogClient.CheckTitleExistsAsync"/>.
/// A value type so the common success path incurs no heap allocation.
/// </summary>
public readonly record struct CatalogTitleCheckResult(CatalogTitleCheckStatus Status)
{
    /// <summary>Returns <see langword="true"/> when Catalog confirmed the title exists.</summary>
    public bool IsFound             => Status == CatalogTitleCheckStatus.Found;

    /// <summary>Returns <see langword="true"/> when Catalog confirmed the title does not exist.</summary>
    public bool IsNotFound          => Status == CatalogTitleCheckStatus.NotFound;

    /// <summary>Returns <see langword="true"/> when the Catalog call failed.</summary>
    public bool IsDependencyFailure => Status == CatalogTitleCheckStatus.DependencyFailure;

    /// <summary>Creates a result indicating the title was found in Catalog.</summary>
    public static CatalogTitleCheckResult Found()             => new(CatalogTitleCheckStatus.Found);

    /// <summary>Creates a result indicating the title was not found in Catalog.</summary>
    public static CatalogTitleCheckResult NotFound()          => new(CatalogTitleCheckStatus.NotFound);

    /// <summary>Creates a result indicating the Catalog call failed.</summary>
    public static CatalogTitleCheckResult DependencyFailure() => new(CatalogTitleCheckStatus.DependencyFailure);
}
