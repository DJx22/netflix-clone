namespace Catalog.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identity for a <see cref="Catalog.Domain.Aggregates.Title"/> aggregate.
/// </summary>
/// <remarks>
/// A plain <c>string</c> TitleId leaks no domain meaning and makes method
/// signatures ambiguous when multiple string IDs appear together.  Wrapping it
/// here costs nothing at runtime and makes parameter-order mistakes a compile
/// error rather than a runtime bug.
/// </remarks>
public sealed record TitleId
{
    /// <summary>The raw string value of this identifier.</summary>
    public string Value { get; }

    /// <summary>
    /// Initialises a <see cref="TitleId"/> from its raw string representation.
    /// </summary>
    /// <param name="value">A non-empty string that uniquely identifies a title.</param>
    /// <exception cref="Exceptions.TitleValidationException">
    /// Thrown when <paramref name="value"/> is null or whitespace.
    /// </exception>
    public TitleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Exceptions.TitleValidationException(nameof(TitleId), "must not be empty");
        }

        Value = value;
    }

    /// <summary>Generates a new, random <see cref="TitleId"/>.</summary>
    public static TitleId NewId() => new(Guid.NewGuid().ToString());

    /// <inheritdoc />
    public override string ToString() => Value;
}
