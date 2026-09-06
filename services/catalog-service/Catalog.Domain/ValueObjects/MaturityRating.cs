namespace Catalog.Domain.ValueObjects;

/// <summary>
/// A validated maturity rating label, e.g. "G", "PG-13", "TV-MA".
/// </summary>
/// <remarks>
/// The OpenAPI spec intentionally leaves maturity rating as a free string rather
/// than an enum to accommodate both MPAA film ratings and TV Parental Guidelines
/// (episodic content).  We enforce non-emptiness and a length cap here so the
/// aggregate can never persist a blank rating — a gap that would be invisible in
/// a schemaless document store (ADR 0005).
/// </remarks>
public sealed record MaturityRating
{
    /// <summary>Maximum character length for a maturity rating label.</summary>
    public const int MaxLength = 20;

    /// <summary>The rating label value, e.g. "PG-13".</summary>
    public string Value { get; }

    /// <summary>
    /// Initialises a <see cref="MaturityRating"/> from a raw label string.
    /// </summary>
    /// <param name="value">
    /// A non-empty rating label, at most <see cref="MaxLength"/> characters.
    /// </param>
    /// <exception cref="Exceptions.TitleValidationException">
    /// Thrown when <paramref name="value"/> is null, whitespace, or exceeds <see cref="MaxLength"/>.
    /// </exception>
    public MaturityRating(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Exceptions.TitleValidationException(nameof(MaturityRating), "must not be empty");
        }

        if (value.Length > MaxLength)
        {
            throw new Exceptions.TitleValidationException(
                nameof(MaturityRating),
                $"must not exceed {MaxLength} characters");
        }

        Value = value.Trim();
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
