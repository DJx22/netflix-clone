using FluentAssertions;
using Identity.Domain.ValueObjects;

namespace Identity.Tests.Domain.ValueObjects;

public sealed class EmailTests
{
    // --- From: valid inputs ---

    [Fact]
    public void From_ValidAddress_ReturnsEmail()
    {
        var email = Email.From("user@example.com");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void From_MixedCaseAddress_NormalisesToLowerCase()
    {
        // Storage and comparison must be unambiguous (Email.cs comment).
        var email = Email.From("User@Example.COM");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void From_AddressWithLeadingAndTrailingWhitespace_Trims()
    {
        var email = Email.From("  user@example.com  ");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void From_AddressAtMaxLength_Succeeds()
    {
        // 254 chars total: 243-char local + @ + domain.com (10 chars)
        var local = new string('a', 243);
        var address = $"{local}@domain.com";
        address.Length.Should().Be(Email.MaxLength);

        var act = () => Email.From(address);
        act.Should().NotThrow();
    }

    // --- From: invalid inputs (domain invariants) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_NullOrWhiteSpace_ThrowsArgumentException(string? raw)
    {
        var act = () => Email.From(raw!);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void From_AddressExceedingMaxLength_ThrowsArgumentException()
    {
        var tooLong = new string('a', 244) + "@domain.com"; // 255 chars
        tooLong.Length.Should().BeGreaterThan(Email.MaxLength);

        var act = () => Email.From(tooLong);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*exceeds the maximum length*");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@nodomain")]
    [InlineData("noatsign.com")]
    [InlineData("user@")]
    public void From_MalformedAddress_ThrowsArgumentException(string raw)
    {
        var act = () => Email.From(raw);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*not a valid email address*");
    }

    // --- Equality ---

    [Fact]
    public void Equals_SameAddressDifferentCase_ReturnsTrue()
    {
        // Case-insensitive equality ensures no duplicate accounts through casing tricks.
        var a = Email.From("user@example.com");
        var b = Email.From("USER@EXAMPLE.COM");
        a.Should().Be(b);
    }

    [Fact]
    public void Equals_DifferentAddresses_ReturnsFalse()
    {
        var a = Email.From("alice@example.com");
        var b = Email.From("bob@example.com");
        a.Should().NotBe(b);
    }
}
