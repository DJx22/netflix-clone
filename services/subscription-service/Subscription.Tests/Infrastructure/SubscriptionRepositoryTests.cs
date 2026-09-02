using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using FluentAssertions;
using Subscription.Domain.Enums;
using Subscription.Infrastructure.Persistence;

namespace Subscription.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="SubscriptionRepository"/> against a real SQL Server container.
/// §14: "Infrastructure gets integration tests against a real (containerised) database, not mocks."
///
/// Each test uses unique AccountIds (random GUIDs) to avoid state bleed between tests.
/// When a test needs multiple contexts (load → mutate → verify), it uses separate
/// <see cref="SubscriptionDbContext"/> instances to ensure persistence actually occurred —
/// not just that in-memory change tracking remembered the mutation.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SubscriptionRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public SubscriptionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── AddAsync / FindByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NewSubscription_CanBeFoundById()
    {
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId);

        await using var addCtx = _fixture.CreateContext();
        var addRepo = new SubscriptionRepository(addCtx);
        await addRepo.AddAsync(sub);

        await using var findCtx = _fixture.CreateContext();
        var found = await new SubscriptionRepository(findCtx).FindByIdAsync(sub.SubscriptionId);

        found.Should().NotBeNull();
        found!.SubscriptionId.Should().Be(sub.SubscriptionId);
        found.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await new SubscriptionRepository(ctx).FindByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // ── FindByAccountIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task FindByAccountIdAsync_KnownAccount_ReturnsMostRecentSubscription()
    {
        // Repository orders by CreatedAtUtc descending — the newer subscription is returned.
        var accountId = Guid.NewGuid();

        // Insert the older subscription first; use explicit timestamps to guarantee ordering.
        var older = new SubscriptionAggregate(
            Guid.NewGuid(), accountId, "basic",
            createdAtUtc: DateTime.UtcNow.AddMinutes(-10));
        var newer = new SubscriptionAggregate(
            Guid.NewGuid(), accountId, "premium",
            createdAtUtc: DateTime.UtcNow);

        await using var seedCtx = _fixture.CreateContext();
        var seedRepo = new SubscriptionRepository(seedCtx);
        await seedRepo.AddAsync(older);
        await seedRepo.AddAsync(newer);

        await using var findCtx = _fixture.CreateContext();
        var found = await new SubscriptionRepository(findCtx).FindByAccountIdAsync(accountId);

        found.Should().NotBeNull();
        found!.SubscriptionId.Should().Be(newer.SubscriptionId);
    }

    [Fact]
    public async Task FindByAccountIdAsync_UnknownAccount_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var result = await new SubscriptionRepository(ctx).FindByAccountIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // ── FindActiveOrPendingByAccountIdAsync ───────────────────────────────────

    [Fact]
    public async Task FindActiveOrPendingByAccountIdAsync_HasActiveSubscription_ReturnsThatSubscription()
    {
        // 409 guard: the method is used to block duplicate-subscription creation.
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId);

        // Seed as PendingPayment, then activate via UpdateAsync (tracks the mutation).
        await using var seedCtx = _fixture.CreateContext();
        var seedRepo = new SubscriptionRepository(seedCtx);
        await seedRepo.AddAsync(sub);

        await using var activateCtx = _fixture.CreateContext();
        var loaded = await new SubscriptionRepository(activateCtx).FindByIdAsync(sub.SubscriptionId);
        loaded!.Activate();
        await new SubscriptionRepository(activateCtx).UpdateAsync(loaded);

        await using var queryCtx = _fixture.CreateContext();
        var found = await new SubscriptionRepository(queryCtx)
            .FindActiveOrPendingByAccountIdAsync(accountId);

        found.Should().NotBeNull();
        found!.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task FindActiveOrPendingByAccountIdAsync_HasPendingSubscription_ReturnsThatSubscription()
    {
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId); // starts as PendingPayment

        await using var seedCtx = _fixture.CreateContext();
        await new SubscriptionRepository(seedCtx).AddAsync(sub);

        await using var queryCtx = _fixture.CreateContext();
        var found = await new SubscriptionRepository(queryCtx)
            .FindActiveOrPendingByAccountIdAsync(accountId);

        found.Should().NotBeNull();
        found!.Status.Should().Be(SubscriptionStatus.PendingPayment);
    }

    [Fact]
    public async Task FindActiveOrPendingByAccountIdAsync_HasOnlyCancelledSubscription_ReturnsNull()
    {
        // A cancelled subscription must not block creating a new one.
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId);

        await using var seedCtx = _fixture.CreateContext();
        await new SubscriptionRepository(seedCtx).AddAsync(sub);

        // Activate then cancel so the subscription is Cancelled in the DB.
        await using var cancelCtx = _fixture.CreateContext();
        var loaded = await new SubscriptionRepository(cancelCtx).FindByIdAsync(sub.SubscriptionId);
        loaded!.Activate();
        loaded.Cancel();
        await new SubscriptionRepository(cancelCtx).UpdateAsync(loaded);

        await using var queryCtx = _fixture.CreateContext();
        var found = await new SubscriptionRepository(queryCtx)
            .FindActiveOrPendingByAccountIdAsync(accountId);

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindActiveOrPendingByAccountIdAsync_NoSubscriptionsForAccount_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var found = await new SubscriptionRepository(ctx)
            .FindActiveOrPendingByAccountIdAsync(Guid.NewGuid());
        found.Should().BeNull();
    }

    // ── UpdateAsync (status transitions) ─────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AfterActivate_PersistsActiveStatus()
    {
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId);

        await using var seedCtx = _fixture.CreateContext();
        await new SubscriptionRepository(seedCtx).AddAsync(sub);

        await using var updateCtx = _fixture.CreateContext();
        var loaded = await new SubscriptionRepository(updateCtx).FindByIdAsync(sub.SubscriptionId);
        loaded!.Activate();
        await new SubscriptionRepository(updateCtx).UpdateAsync(loaded);

        await using var verifyCtx = _fixture.CreateContext();
        var verified = await new SubscriptionRepository(verifyCtx).FindByIdAsync(sub.SubscriptionId);
        verified!.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task UpdateAsync_AfterCancel_PersistsCancelledStatus()
    {
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId);

        await using var seedCtx = _fixture.CreateContext();
        await new SubscriptionRepository(seedCtx).AddAsync(sub);

        // Activate then cancel in separate contexts to mirror real usage.
        await using var activateCtx = _fixture.CreateContext();
        var forActivate = await new SubscriptionRepository(activateCtx).FindByIdAsync(sub.SubscriptionId);
        forActivate!.Activate();
        await new SubscriptionRepository(activateCtx).UpdateAsync(forActivate);

        await using var cancelCtx = _fixture.CreateContext();
        var forCancel = await new SubscriptionRepository(cancelCtx).FindByIdAsync(sub.SubscriptionId);
        forCancel!.Cancel();
        await new SubscriptionRepository(cancelCtx).UpdateAsync(forCancel);

        await using var verifyCtx = _fixture.CreateContext();
        var verified = await new SubscriptionRepository(verifyCtx).FindByIdAsync(sub.SubscriptionId);
        verified!.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task UpdateAsync_AfterChangePlan_PersistsNewPlanId()
    {
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId, "basic");

        await using var seedCtx = _fixture.CreateContext();
        await new SubscriptionRepository(seedCtx).AddAsync(sub);

        await using var activateCtx = _fixture.CreateContext();
        var loaded = await new SubscriptionRepository(activateCtx).FindByIdAsync(sub.SubscriptionId);
        loaded!.Activate();
        await new SubscriptionRepository(activateCtx).UpdateAsync(loaded);

        await using var changeCtx = _fixture.CreateContext();
        var forChange = await new SubscriptionRepository(changeCtx).FindByIdAsync(sub.SubscriptionId);
        forChange!.ChangePlan("premium");
        await new SubscriptionRepository(changeCtx).UpdateAsync(forChange);

        await using var verifyCtx = _fixture.CreateContext();
        var verified = await new SubscriptionRepository(verifyCtx).FindByIdAsync(sub.SubscriptionId);
        verified!.PlanId.Should().Be("premium");
    }

    // ── UTC kind assertion (SubscriptionConfiguration round-trip) ────────────

    [Fact]
    public async Task FindByIdAsync_RoundTrip_CurrentPeriodEndUtcHasUtcKind()
    {
        // SubscriptionConfiguration re-asserts UTC kind after SQL Server strips it.
        var accountId = Guid.NewGuid();
        var sub       = CreateSubscription(accountId);

        await using var seedCtx = _fixture.CreateContext();
        await new SubscriptionRepository(seedCtx).AddAsync(sub);

        await using var verifyCtx = _fixture.CreateContext();
        var loaded = await new SubscriptionRepository(verifyCtx).FindByIdAsync(sub.SubscriptionId);

        loaded!.CurrentPeriodEndUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubscriptionAggregate CreateSubscription(Guid accountId, string planId = "basic") =>
        new(Guid.NewGuid(), accountId, planId, DateTime.UtcNow);
}
