using FluentAssertions;
using Subscription.Domain.Entities;
using Subscription.Domain.ValueObjects;

namespace Subscription.Tests.Domain.Entities;

/// <summary>
/// Tests every constructor guard on the <see cref="Plan"/> reference entity.
/// </summary>
public sealed class PlanTests
{
    private static Money         ValidPrice   => new(9.99m);
    private static VideoQuality  ValidQuality => new("HD");

    // ── Constructor guards ────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespacePlanId_ThrowsArgumentException(string planId)
    {
        var act = () => new Plan(planId, "Basic", ValidPrice, 1, ValidQuality);
        act.Should().Throw<ArgumentException>().WithParameterName("planId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceName_ThrowsArgumentException(string name)
    {
        var act = () => new Plan("basic", name, ValidPrice, 1, ValidQuality);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_MaxProfilesLessThanOne_ThrowsArgumentOutOfRangeException(int maxProfiles)
    {
        // A plan that allows zero profiles cannot be used by any account (Plan.cs comment).
        var act = () => new Plan("basic", "Basic", ValidPrice, maxProfiles, ValidQuality);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxProfiles");
    }

    [Fact]
    public void Constructor_ValidArguments_SetsAllProperties()
    {
        var plan = new Plan("basic", "Basic Plan", ValidPrice, 2, ValidQuality, "A starter plan.");

        plan.PlanId.Should().Be("basic");
        plan.Name.Should().Be("Basic Plan");
        plan.PriceMonthly.Should().Be(ValidPrice);
        plan.MaxProfiles.Should().Be(2);
        plan.VideoQuality.Should().Be(ValidQuality);
        plan.Description.Should().Be("A starter plan.");
    }

    [Fact]
    public void Constructor_NullDescription_SetsDescriptionToNull()
    {
        var plan = new Plan("basic", "Basic Plan", ValidPrice, 1, ValidQuality);
        plan.Description.Should().BeNull();
    }

    // ── Money value object ────────────────────────────────────────────────────

    [Fact]
    public void Money_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new Money(-0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("amount");
    }

    [Fact]
    public void Money_ZeroAmount_IsAllowed()
    {
        // Free plans (amount = 0) are valid; only negative prices are rejected.
        var act = () => new Money(0m);
        act.Should().NotThrow();
    }

    // ── VideoQuality value object ─────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VideoQuality_NullOrWhitespace_ThrowsArgumentException(string value)
    {
        var act = () => new VideoQuality(value);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void VideoQuality_EqualityIsCaseInsensitive()
    {
        // Domain comment: "hd" and "HD" are the same tier.
        new VideoQuality("HD").Should().Be(new VideoQuality("hd"));
    }
}
