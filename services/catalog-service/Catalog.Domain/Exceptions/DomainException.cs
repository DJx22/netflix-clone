namespace Catalog.Domain.Exceptions;

/// <summary>
/// Base type for all invariant violations raised within the Catalog domain.
/// </summary>
/// <remarks>
/// Separating domain exceptions from infrastructure/application exceptions lets
/// the global handler map them to 422/400 without catching System.Exception broadly
/// (§10, §16).  Concrete subclasses carry the specific violated rule.
/// </remarks>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
