using MediatR;

namespace Catalog.Application.Commands;

/// <summary>
/// Permanently removes a title from the catalog.
/// Returns <see cref="Unit"/> — the spec's DELETE 204 carries no body.
/// </summary>
public sealed record DeleteTitleCommand(string TitleId) : IRequest<Unit>;
