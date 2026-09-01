using FluentAssertions;
using Profile.Domain.ValueObjects;

namespace Profile.Tests.Domain.ValueObjects;

public sealed class DisplayNameTests
{
    [Fact]
    public void From_ValidName_ReturnsDisplayName()
    {
        var name = DisplayName.From("Alice");
        name.Value.Should().Be("Alice");
    }

    [Fact]
    public void From_ValidName_TrimsWhitespace()
    {
        var name = DisplayName.From("  Alice  ");
        name.Value.Should().Be("Alice");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void From_EmptyOrWhitespace_ThrowsArgumentException(string? value)
    {
        // openapi.yaml: displayName is required.
        var act = () => DisplayName.From(value!);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*empty*");
    }

    [Fact]
    public void From_ExactlyMaxLength_Succeeds()
    {
        // Boundary: exactly 40 characters must be accepted.
        var exactly40 = new string('A', DisplayName.MaxLength);
        var name = DisplayName.From(exactly40);
        name.Value.Should().HaveLength(DisplayName.MaxLength);
    }

    [Fact]
    public void From_ExceedsMaxLength_ThrowsArgumentException()
    {
        // openapi.yaml: maxLength 40.
        var tooLong = new string('A', DisplayName.MaxLength + 1);
        var act = () => DisplayName.From(tooLong);
        act.Should().Throw<ArgumentException>()
           .WithMessage($"*{DisplayName.MaxLength}*");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        DisplayName.From("Bob").ToString().Should().Be("Bob");
    }
}
