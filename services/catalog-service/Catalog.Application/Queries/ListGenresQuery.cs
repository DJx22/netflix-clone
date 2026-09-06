using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Returns the distinct set of genre labels present in the catalog.
/// Maps to <c>GET /api/v1/genres</c>.
/// </summary>
/// <remarks>
/// No parameters — the spec returns every known genre with no filtering or pagination.
/// MediatR still routes this through the pipeline so the logging behaviour fires
/// for observability (§13).
/// </remarks>
public sealed record ListGenresQuery : IRequest<IReadOnlyList<string>>;
