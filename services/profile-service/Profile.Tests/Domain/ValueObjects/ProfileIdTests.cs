using FluentAssertions;
using Profile.Domain.ValueObjects;

namespace Profile.Tests.Domain.ValueObjects;

public sealed class ProfileIdTests
{
    [Fact]
    public void New_ReturnsDifferentIdEachCall()
    {
        var a = ProfileId.New();
        var b = ProfileId.New();
        a.Should().NotBe(b);
    }

    [Fact]
    public void From_ValidGuid_ReturnsProfileId()
    {
        var guid = Guid.NewGuid();
        var id = ProfileId.From(guid);
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void From_EmptyGuid_ThrowsArgumentException()
    {
        // ADR 0001: Guid is the ID strategy — empty Guid signals an uninitialized value.
        var act = () => ProfileId.From(Guid.Empty);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*empty Guid*");
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        ProfileId.From(guid).ToString().Should().Be(guid.ToString());
    }
}
