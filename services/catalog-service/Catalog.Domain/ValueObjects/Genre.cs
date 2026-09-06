namespace Catalog.Domain.ValueObjects;

/// <summary>
/// A validated genre label, e.g. "Action", "Drama".
/// </summary>
/// <remarks>
/// Genres are stored as strings in MongoDB (per ADR 0005's document shape) and
/// the OpenAPI spec treats them as plain strings, but we still validate them in
/// the domain so the aggregate can never hold an empty or whitespace genre — a
/// constraint that has no natural enforcement point in Infrastructure.
/// </remarks>
public sealed record Genre
{
    /// <summary>Maximum character length for a genre label.</summary>
    public const int MaxLength = 100;

    /// <summary>The genre label value.</summary>
    public string Value { get; }

    /// <summary>
    /// Initialises a <see cref="Genre"/> from a raw label string.
    /// </summary>
    /// <param name="value">A non-empty genre label, at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="Exceptions.TitleValidationException">
    /// Thrown when <paramref name="value"/> is null, whitespace, or exceeds <see cref="MaxLength"/>.
    /// </exception>
    public Genre(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Exceptions.TitleValidationException(nameof(Genre), "must not be empty");
        }

        if (value.Length > MaxLength)
        {
            throw new Exceptions.TitleValidationException(
                nameof(Genre),
                $"must not exceed {MaxLength} characters");
        }

        Value = value.Trim();
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
