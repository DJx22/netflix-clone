namespace Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed wrapper for the User surrogate key.
/// A Guid is used as the ID strategy per ADR 0001: it avoids sequential leakage,
/// works without a database round-trip at creation time, and is portable if the
/// single SQL Server container is ever split into per-service containers.
/// </summary>
public readonly record struct UserId
{
    public Guid Value { get; }

    private UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be an empty Guid.", nameof(value));
        }

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
