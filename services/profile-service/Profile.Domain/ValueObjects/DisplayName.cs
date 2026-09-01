namespace Profile.Domain.ValueObjects;

/// <summary>
/// The user-visible name shown on the profile tile.
/// Invariants from the spec (openapi.yaml CreateProfileRequest / UpdateProfileRequest):
///   - Required (not null, not whitespace-only).
///   - Maximum 40 characters.
/// </summary>
public readonly record struct DisplayName
{
    public const int MaxLength = 40;

    public string Value { get; }

    private DisplayName(string value)
    {
        Value = value;
    }

    public static DisplayName From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Display name cannot exceed {MaxLength} characters.",
                nameof(value));
        }

        return new DisplayName(value.Trim());
    }

    public override string ToString() => Value;
}
