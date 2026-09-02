using FluentAssertions;
using Subscription.Domain.Entities;
using Subscription.Domain.ValueObjects;
using Subscription.Infrastructure.Persistence;

namespace Subscription.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="PlanRepository"/> against a real SQL Server container.
///
/// <b>Note on plan seeding</b>: the <see cref="PlanRepository"/> has no <c>AddAsync</c>
/// method — plans are reference data seeded via migrations in production, never created
/// via the API. In these tests, plans are inserted directly through the DbContext to keep
/// tests self-contained without a repository method that doesn't belong in production code.
///
/// Each test uses unique PlanIds to avoid conflicts with other tests in the shared container.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlanRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public PlanRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── FindByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FindByIdAsync_KnownPlanId_ReturnsPlan()
    {
        var planId = "find-by-id-plan";
        await SeedPlan(planId, "Find-By-Id Plan", 9.99m, 1, "SD");

        await using var ctx = _fixture.CreateContext();
        var found = await new PlanRepository(ctx).FindByIdAsync(planId);

        found.Should().NotBeNull();
        found!.PlanId.Should().Be(planId);
        found.Name.Should().Be("Find-By-Id Plan");
        found.PriceMonthly.Amount.Should().Be(9.99m);
        found.MaxProfiles.Should().Be(1);
        found.VideoQuality.Value.Should().Be("SD");
    }

    [Fact]
    public async Task FindByIdAsync_UnknownPlanId_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await new PlanRepository(ctx).FindByIdAsync("ghost-plan");
        result.Should().BeNull();
    }

    // ── ListAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAllAsync_WithSeededPlans_ContainsThem()
    {
        // Use unique IDs so this test is independent of other tests' data.
        await SeedPlan("list-basic",   "List Basic",   8.99m, 1, "SD");
        await SeedPlan("list-premium", "List Premium", 17.99m, 4, "4K");

        await using var ctx = _fixture.CreateContext();
        var result = await new PlanRepository(ctx).ListAllAsync();

        result.Should().Contain(p => p.PlanId == "list-basic");
        result.Should().Contain(p => p.PlanId == "list-premium");
    }

    [Fact]
    public async Task FindByIdAsync_RoundTrip_PreservesAllFields()
    {
        // Verifies that PlanConfiguration's Money/VideoQuality converters round-trip correctly.
        var planId = "round-trip-plan";
        await SeedPlan(planId, "Round Trip Plan", 12.34m, 3, "HD", "A test plan.");

        await using var ctx = _fixture.CreateContext();
        var found = await new PlanRepository(ctx).FindByIdAsync(planId);

        found.Should().NotBeNull();
        found!.PriceMonthly.Amount.Should().Be(12.34m);
        found.MaxProfiles.Should().Be(3);
        found.VideoQuality.Value.Should().Be("HD");
        found.Description.Should().Be("A test plan.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SeedPlan(
        string planId,
        string name,
        decimal priceMonthly,
        int maxProfiles,
        string videoQuality,
        string? description = null)
    {
        var plan = new Plan(
            planId, name,
            new Money(priceMonthly),
            maxProfiles,
            new VideoQuality(videoQuality),
            description);

        await using var ctx = _fixture.CreateContext();
        // Skip if already seeded by a previous test run in the same container session.
        if (await ctx.Plans.FindAsync(planId) is null)
        {
            ctx.Plans.Add(plan);
            await ctx.SaveChangesAsync();
        }
    }
}
