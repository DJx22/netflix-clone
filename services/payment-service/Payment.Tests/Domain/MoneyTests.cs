using Payment.Domain.ValueObjects;

namespace Payment.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Money"/> value object.
/// Covers every invariant stated in the domain source and openapi.yaml (amount as non-negative float;
/// currency as ISO 4217 three-letter code).
/// </summary>
public sealed class MoneyTests
{
    // ── Constructor: amount invariants ────────────────────────────────────────

    [Fact]
    public void Constructor_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const decimal negativeAmount = -0.01m;

        // Act
        Action act = () => _ = new Money(negativeAmount, "USD");

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Constructor_ZeroAmount_DoesNotThrow()
    {
        // Arrange / Act / Assert
        // Zero is explicitly permitted: "zero represents a free-tier or trial charge"
        var money = new Money(0m, "USD");
        Assert.Equal(0m, money.Amount);
    }

    [Theory]
    [InlineData("9.999")]   // 3 decimal places
    [InlineData("1.001")]   // 3 decimal places
    [InlineData("0.001")]   // 3 decimal places
    public void Constructor_AmountWithMoreThanTwoDecimalPlaces_ThrowsArgumentException(string rawAmount)
    {
        // Arrange
        var amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        Action act = () => _ = new Money(amount, "USD");

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData("9.99")]   // exactly 2 decimal places
    [InlineData("10.00")]  // exactly 2 decimal places (trailing zero)
    [InlineData("100")]    // whole number — 0 decimal places
    [InlineData("0.10")]   // 2 decimal places
    public void Constructor_AmountWithAtMostTwoDecimalPlaces_DoesNotThrow(string rawAmount)
    {
        // Arrange
        var amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var money = new Money(amount, "USD");

        // Assert
        Assert.Equal(amount, money.Amount);
    }

    [Fact]
    public void Constructor_PositiveAmount_SetsAmount()
    {
        // Arrange / Act
        var money = new Money(9.99m, "USD");

        // Assert
        Assert.Equal(9.99m, money.Amount);
    }

    // ── Constructor: currency invariants ──────────────────────────────────────

    [Fact]
    public void Constructor_NullCurrency_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new Money(10m, null!);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_EmptyCurrency_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new Money(10m, string.Empty);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_WhitespaceCurrency_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new Money(10m, "   ");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("US")]       // too short
    [InlineData("USDD")]     // too long
    [InlineData("U")]        // single char
    public void Constructor_CurrencyNotThreeChars_ThrowsArgumentException(string currency)
    {
        // Arrange / Act
        Action act = () => _ = new Money(10m, currency);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_ValidThreeCharCurrency_SetsCurrencyUpperCase()
    {
        // Arrange / Act — lowercase input must be normalised to upper-case
        var money = new Money(10m, "usd");

        // Assert
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Constructor_UpperCaseCurrency_SetsCurrency()
    {
        // Arrange / Act
        var money = new Money(10m, "EUR");

        // Assert
        Assert.Equal("EUR", money.Currency);
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void Equals_SameAmountAndCurrency_ReturnsTrue()
    {
        // Arrange
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");

        // Act / Assert
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentAmount_ReturnsFalse()
    {
        // Arrange
        var a = new Money(10m, "USD");
        var b = new Money(11m, "USD");

        // Act / Assert
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentCurrency_ReturnsFalse()
    {
        // Arrange
        var a = new Money(10m, "USD");
        var b = new Money(10m, "EUR");

        // Act / Assert
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        // Arrange
        var a = new Money(10m, "USD");

        // Act / Assert
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        // Arrange
        var a = new Money(5m, "INR");
        var b = new Money(5m, "INR");

        // Act / Assert
        Assert.True(a == b);
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        // Arrange
        var a = new Money(5m, "INR");
        var b = new Money(6m, "INR");

        // Act / Assert
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");

        // Act / Assert
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ReturnsAmountAndCurrencyFormatted()
    {
        // Arrange
        var money = new Money(12.5m, "USD");

        // Act
        var result = money.ToString();

        // Assert
        Assert.Equal("12.50 USD", result);
    }
}
