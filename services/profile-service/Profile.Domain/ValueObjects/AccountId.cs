namespace Profile.Domain.ValueObjects;

/// <summary>
/// Strongly-typed reference to the owning account (Identity bounded context).
/// Profile stores this as a foreign key — it does not own or manage the Account itself.
/// </summary>
public readonly record struct AccountId
{
    public Guid Value { get; }

    private AccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("AccountId cannot be an empty Guid.", nameof(value));
        }

        Value = value;
    }

    public static AccountId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
