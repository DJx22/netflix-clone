using FluentAssertions;
using Profile.Domain.ValueObjects;

namespace Profile.Tests.Domain.ValueObjects;

public sealed class AccountIdTests
{
    [Fact]
    public void From_ValidGuid_ReturnsAccountId()
    {
        var guid = Guid.NewGuid();
        var id = AccountId.From(guid);
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void From_EmptyGuid_ThrowsArgumentException()
    {
        var act = () => AccountId.From(Guid.Empty);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*empty Guid*");
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        AccountId.From(guid).ToString().Should().Be(guid.ToString());
    }
}
