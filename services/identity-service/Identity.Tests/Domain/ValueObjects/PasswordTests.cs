using FluentAssertions;
using Identity.Domain.ValueObjects;

namespace Identity.Tests.Domain.ValueObjects;

public sealed class PasswordTests
{
    // --- From: valid inputs ---

    [Fact]
    public void From_ValidPassword_ReturnsPassword()
    {
        var password = Password.From("secret1234");
        password.Value.Should().Be("secret1234");
    }

    [Fact]
    public void From_ExactlyMinLength_WithDigit_Succeeds()
    {
        // Boundary: exactly 10 characters including one digit (openapi.yaml line 215-216).
        var act = () => Password.From("aaaaaaaaa1");
        act.Should().NotThrow();
    }

    [Fact]
    public void From_LongerThanMinLength_WithDigit_Succeeds()
    {
        var act = () => Password.From("a-long-valid-password1");
        act.Should().NotThrow();
    }

    // --- From: invalid inputs (domain invariants from openapi.yaml) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void From_NullOrEmpty_ThrowsArgumentException(string? raw)
    {
        var act = () => Password.From(raw!);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot be empty*");
    }

    [Theory]
    [InlineData("short1")]   // 6 chars
    [InlineData("short12")]  // 7 chars
    [InlineData("aaaaaa11")] // 8 chars
    [InlineData("aaaaaaa1")] // 9 chars — one below boundary
    public void From_FewerThanTenCharacters_ThrowsArgumentException(string raw)
    {
        raw.Length.Should().BeLessThan(Password.MinLength);
        var act = () => Password.From(raw);
        act.Should().Throw<ArgumentException>()
           .WithMessage($"*at least {Password.MinLength}*");
    }

    [Fact]
    public void From_TenOrMoreCharactersWithNoDigit_ThrowsArgumentException()
    {
        // Spec: at least one digit required (openapi.yaml line 216).
        var act = () => Password.From("nodigitshere");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*at least one number*");
    }

    [Fact]
    public void From_ExactlyNineCharactersWithDigit_ThrowsArgumentException()
    {
        // Below minimum length — digit rule is irrelevant when length fails first.
        var act = () => Password.From("aaaaaaaa1");
        act.Should().Throw<ArgumentException>();
    }
}
