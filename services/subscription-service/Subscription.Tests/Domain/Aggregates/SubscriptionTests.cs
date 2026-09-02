using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using FluentAssertions;
using Subscription.Domain.Enums;
using Subscription.Domain.Exceptions;

namespace Subscription.Tests.Domain.Aggregates;

/// <summary>
/// Tests every constructor guard and state-machine transition on the
/// <see cref="SubscriptionAggregate"/> aggregate root.
/// §14: one logical assertion focus per test. Comments cite the spec or ADR
/// that establishes each rule so gaps are visible if the spec changes.
/// </summary>
public sealed class SubscriptionTests
{
    private static readonly Guid   ValidId        = Guid.NewGuid();
    private static readonly Guid   ValidAccountId = Guid.NewGuid();
    private const           string ValidPlanId    = "basic";
    private static readonly DateTime UtcNow      = DateTime.UtcNow;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubscriptionAggregate MakePending() =>
        new(ValidId, ValidAccountId, ValidPlanId, UtcNow);

    private static SubscriptionAggregate MakeActive()
    {
        var sub = MakePending();
        sub.Activate();
        return sub;
    }

    private static SubscriptionAggregate MakePastDue()
    {
        var sub = MakeActive();
        sub.MarkPastDue();
        return sub;
    }

    private static SubscriptionAggregate MakeCancelled()
    {
        var sub = MakeActive();
        sub.Cancel();
        return sub;
    }

    // ── Constructor — argument guards ─────────────────────────────────────────

    [Fact]
    public void Constructor_EmptySubscriptionId_ThrowsArgumentException()
    {
        var act = () => new SubscriptionAggregate(Guid.Empty, ValidAccountId, ValidPlanId, UtcNow);
        act.Should().Throw<ArgumentException>().WithParameterName("subscriptionId");
    }

    [Fact]
    public void Constructor_EmptyAccountId_ThrowsArgumentException()
    {
        var act = () => new SubscriptionAggregate(ValidId, Guid.Empty, ValidPlanId, UtcNow);
        act.Should().Throw<ArgumentException>().WithParameterName("accountId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespacePlanId_ThrowsArgumentException(string planId)
    {
        var act = () => new SubscriptionAggregate(ValidId, ValidAccountId, planId, UtcNow);
        act.Should().Throw<ArgumentException>().WithParameterName("planId");
    }

    [Fact]
    public void Constructor_NonUtcCreatedAt_ThrowsArgumentException()
    {
        // The domain must not rely on the caller correctly specifying Kind — it enforces it.
        var localNow = DateTime.Now;
        var act = () => new SubscriptionAggregate(ValidId, ValidAccountId, ValidPlanId, localNow);
        act.Should().Throw<ArgumentException>().WithParameterName("createdAtUtc");
    }

    [Fact]
    public void Constructor_ValidArguments_SetsPropertiesCorrectly()
    {
        var sub = MakePending();
        sub.SubscriptionId.Should().Be(ValidId);
        sub.AccountId.Should().Be(ValidAccountId);
        sub.PlanId.Should().Be(ValidPlanId);
    }

    [Fact]
    public void Constructor_ValidArguments_SetsStatusToPendingPayment()
    {
        // openapi.yaml: POST /subscriptions returns 202; subscription starts in PendingPayment.
        MakePending().Status.Should().Be(SubscriptionStatus.PendingPayment);
    }

    [Fact]
    public void Constructor_ValidArguments_SeedsPeriodEndToCreatedAtPlusOneMonth()
    {
        // Monthly cadence inferred from spec's priceMonthly field.
        var sub = MakePending();
        sub.CurrentPeriodEndUtc.Should().Be(UtcNow.AddMonths(1));
    }

    // ── Computed boolean properties ────────────────────────────────────────────

    [Fact]
    public void IsPendingPayment_WhenStatusIsPendingPayment_ReturnsTrue()
        => MakePending().IsPendingPayment.Should().BeTrue();

    [Fact]
    public void IsActive_WhenStatusIsActive_ReturnsTrue()
        => MakeActive().IsActive.Should().BeTrue();

    [Fact]
    public void IsPastDue_WhenStatusIsPastDue_ReturnsTrue()
        => MakePastDue().IsPastDue.Should().BeTrue();

    [Fact]
    public void IsCancelled_WhenStatusIsCancelled_ReturnsTrue()
        => MakeCancelled().IsCancelled.Should().BeTrue();

    // ── Activate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_WhenPendingPayment_TransitionsToActive()
    {
        // ADR 0003 + openapi.yaml: PaymentCompleted event activates the subscription.
        var sub = MakePending();
        sub.Activate();
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotentAndDoesNotThrow()
    {
        // ADR 0004: idempotent — a duplicate PaymentCompleted delivery must not fault the consumer.
        var sub = MakeActive();
        var act = () => sub.Activate();
        act.Should().NotThrow();
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Activate_WhenCancelled_ThrowsInvalidSubscriptionOperationException()
    {
        // Cancelled is a terminal state — cannot be reactivated.
        var sub = MakeCancelled();
        var act = () => sub.Activate();
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    [Fact]
    public void Activate_WhenPastDue_ThrowsInvalidSubscriptionOperationException()
    {
        // PastDue is reached by a billing job, not by a PaymentCompleted event for a fresh payment.
        var sub = MakePastDue();
        var act = () => sub.Activate();
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenActive_TransitionsToCancelled()
    {
        // openapi.yaml DELETE /subscriptions/me: cancellation takes effect at currentPeriodEndUtc.
        var sub = MakeActive();
        sub.Cancel();
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenPastDue_TransitionsToCancelled()
    {
        // A past-due account should still be able to cancel rather than being locked in PastDue.
        var sub = MakePastDue();
        sub.Cancel();
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_ThrowsInvalidSubscriptionOperationException()
    {
        // Cancelling before payment confirmation makes no sense — the subscription isn't active.
        var sub = MakePending();
        var act = () => sub.Cancel();
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsInvalidSubscriptionOperationException()
    {
        // Cancelled is terminal — double-cancel must be rejected.
        var sub = MakeCancelled();
        var act = () => sub.Cancel();
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    // ── ChangePlan ────────────────────────────────────────────────────────────

    [Fact]
    public void ChangePlan_WhenActive_UpdatesPlanId()
    {
        // openapi.yaml PUT /subscriptions/me/plan: change plan on an active subscription.
        var sub = MakeActive();
        sub.ChangePlan("premium");
        sub.PlanId.Should().Be("premium");
    }

    [Fact]
    public void ChangePlan_WhenPastDue_UpdatesPlanId()
    {
        // Spec does not restrict plan changes for PastDue (gap note: no explicit rule).
        // The domain allows it; a billing job can still process the overdue payment.
        var sub = MakePastDue();
        sub.ChangePlan("standard");
        sub.PlanId.Should().Be("standard");
    }

    [Fact]
    public void ChangePlan_WhenPendingPayment_ThrowsInvalidSubscriptionOperationException()
    {
        // openapi.yaml: changing plan before payment confirmation is meaningless.
        var sub = MakePending();
        var act = () => sub.ChangePlan("premium");
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    [Fact]
    public void ChangePlan_WhenCancelled_ThrowsInvalidSubscriptionOperationException()
    {
        // Cancelled is terminal — a new subscription must be created.
        var sub = MakeCancelled();
        var act = () => sub.ChangePlan("premium");
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePlan_EmptyNewPlanId_ThrowsArgumentException(string newPlanId)
    {
        var sub = MakeActive();
        var act = () => sub.ChangePlan(newPlanId);
        act.Should().Throw<ArgumentException>().WithParameterName("newPlanId");
    }

    // ── MarkPastDue ───────────────────────────────────────────────────────────

    [Fact]
    public void MarkPastDue_WhenActive_TransitionsToPastDue()
    {
        // Domain behaviour for a scheduled billing job (no API endpoint in spec — see gap note below).
        var sub = MakeActive();
        sub.MarkPastDue();
        sub.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public void MarkPastDue_WhenNotActive_ThrowsInvalidSubscriptionOperationException()
    {
        var sub = MakePending();
        var act = () => sub.MarkPastDue();
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }

    // ── RenewPeriod ───────────────────────────────────────────────────────────

    [Fact]
    public void RenewPeriod_WhenActive_UpdatesCurrentPeriodEndUtc()
    {
        var sub      = MakeActive();
        var newEnd   = DateTime.UtcNow.AddMonths(2);
        sub.RenewPeriod(newEnd);
        sub.CurrentPeriodEndUtc.Should().Be(newEnd);
    }

    [Fact]
    public void RenewPeriod_NonUtcDateTime_ThrowsArgumentException()
    {
        var sub = MakeActive();
        var act = () => sub.RenewPeriod(DateTime.Now); // local kind
        act.Should().Throw<ArgumentException>().WithParameterName("newPeriodEndUtc");
    }

    [Fact]
    public void RenewPeriod_WhenNotActive_ThrowsInvalidSubscriptionOperationException()
    {
        var sub = MakePending();
        var act = () => sub.RenewPeriod(DateTime.UtcNow.AddMonths(1));
        act.Should().Throw<InvalidSubscriptionOperationException>();
    }
}

/* ── Spec gap note ────────────────────────────────────────────────────────────
 *
 * MarkPastDue() and RenewPeriod() are domain methods with no corresponding API
 * endpoint in openapi.yaml. They are intended for an internal billing job not yet
 * defined in the spec. They are tested at the domain level to verify the state
 * machine is correct, but there is currently no ISubscriptionService method,
 * controller endpoint, or integration test path that exercises these transitions
 * end-to-end.
 *
 * Similarly, the spec does not explicitly state whether plan changes are permitted
 * from PastDue status. The domain currently allows it (no guard in ChangePlan for
 * PastDue). If the product team decides otherwise, add a guard and a test here.
 *
 * ────────────────────────────────────────────────────────────────────────────── */
