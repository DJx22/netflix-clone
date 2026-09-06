namespace Catalog.Domain.Exceptions;

/// <summary>
/// Raised when an operation targets a <see cref="Catalog.Domain.Aggregates.Title"/>
/// that does not exist.
/// </summary>
/// <remarks>
/// Keeping "not found" separate from validation exceptions lets application-layer
/// handlers distinguish 404 from 400 without inspecting exception messages (§10).
/// </remarks>
public sealed class TitleNotFoundException : DomainException
{
    public TitleNotFoundException(string titleId)
        : base($"No title found with id '{titleId}'.") { }
}
