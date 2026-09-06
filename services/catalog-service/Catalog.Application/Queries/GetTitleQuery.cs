using Catalog.Application.DTOs;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Returns the full detail for a single title by its ID.
/// Maps to <c>GET /api/v1/titles/{titleId}</c>.
/// </summary>
public sealed record GetTitleQuery(string TitleId) : IRequest<TitleDetailDto?>;
