using FluentAssertions;
using Identity.Domain.ValueObjects;

namespace Identity.Tests.Domain.ValueObjects;

public sealed class UserIdTests
{
    [Fact]
    public void New_ReturnsDifferentIdEachCall()
    {
        // IDs must be unique — Guid collision probability is negligible but
        // this test catches any accidental constant/static state bug.
        var a = UserId.New();
        var b = UserId.New();
        a.Should().NotBe(b);
    }

    [Fact]
    public void From_ValidGuid_ReturnsUserId()
    {
        var guid = Guid.NewGuid();
        var userId = UserId.From(guid);
        userId.Value.Should().Be(guid);
    }

    [Fact]
    public void From_EmptyGuid_ThrowsArgumentException()
    {
        // ADR 0001: Guid is the ID strategy — empty Guid signals an uninitialized value.
        var act = () => UserId.From(Guid.Empty);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*empty Guid*");
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        UserId.From(guid).ToString().Should().Be(guid.ToString());
    }
}
