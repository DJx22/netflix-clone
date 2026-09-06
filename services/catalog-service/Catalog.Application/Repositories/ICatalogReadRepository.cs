using Catalog.Application.DTOs;

namespace Catalog.Application.Repositories;

/// <summary>
/// Read-side query contract for the Catalog.
/// Query handlers use this to retrieve data shaped for what the client actually needs,
/// bypassing aggregate materialization entirely.
/// </summary>
/// <remarks>
/// Per ADR 0005's CQRS read path, query handlers project straight from MongoDB to DTO
/// shapes — they never construct a full <c>Title</c> aggregate.  That projection logic
/// belongs in the Infrastructure implementation; this interface carries only the
/// technology-agnostic signature.
///
/// No <c>IMongoCollection&lt;T&gt;</c>, <c>FilterDefinition&lt;T&gt;</c>, or any
/// MongoDB driver type appears here (§2, §16).  Infrastructure owns the mapping.
///
/// Using the same single <c>titles</c> collection as the write side — CQRS here is a
/// code-organisation split, not a materialized-projection architecture.  A separate
/// read collection would be a scope increase that the current read/write shape divergence
/// does not justify.
/// </remarks>
public interface ICatalogReadRepository
{
    /// <summary>
    /// Returns the full detail DTO for a single title, or <c>null</c> when not found.
    /// </summary>
    /// <param name="titleId">Raw string identifier (not <c>TitleId</c> value object — the read side never loads the aggregate).</param>
    Task<TitleDetailDto?> FindDetailByIdAsync(string titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paged list of title summaries, optionally filtered by a free-text
    /// search term and/or a genre label.
    /// </summary>
    /// <param name="search">Optional free-text search term matched against the title name.</param>
    /// <param name="genre">Optional genre filter; must match exactly one of the title's genre labels.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of results per page (1–100).</param>
    Task<PagedResultDto<TitleSummaryDto>> SearchAsync(
        string? search,
        string? genre,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the distinct set of genre labels present across all titles in the catalog.
    /// </summary>
    Task<IReadOnlyList<string>> ListDistinctGenresAsync(CancellationToken cancellationToken = default);
}
