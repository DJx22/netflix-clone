namespace Catalog.Domain.Exceptions;

/// <summary>
/// Raised when a caller supplies a field value that violates a domain constraint
/// (e.g., a blank title name, a release year in the future).
/// </summary>
/// <remarks>
/// Distinct from <see cref="TitleNotFoundException"/> so that infrastructure can
/// map each to a different HTTP status code without examining the message string.
/// </remarks>
public sealed class TitleValidationException : DomainException
{
    /// <summary>The name of the field whose value was rejected.</summary>
    public string FieldName { get; }

    public TitleValidationException(string fieldName, string reason)
        : base($"'{fieldName}' is invalid: {reason}")
    {
        FieldName = fieldName;
    }
}
