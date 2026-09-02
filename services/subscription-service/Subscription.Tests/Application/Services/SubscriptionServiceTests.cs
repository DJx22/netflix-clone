using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using FluentAssertions;
using Moq;
using Subscription.Application.Abstractions;
using Subscription.Application.DTOs;
using Subscription.Application.Exceptions;
using Subscription.Application.Services;
using Subscription.Domain.Entities;
using Subscription.Domain.Exceptions;
using Subscription.Domain.Interfaces;
using Subscription.Domain.ValueObjects;

namespace Subscription.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="SubscriptionService"/>.
/// All dependencies are mocked — no database, no real clock.
/// The point is to verify orchestration: that the service calls repositories in
/// the right order, delegates mutation to the domain, and throws the right exceptions.
/// </summary>
public sealed class SubscriptionServiceTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepoMock = new();
    private readonly Mock<IPlanRepository>         _planRepoMock         = new();
    private readonly Mock<IDateTimeProvider>       _clockMock            = new();
    private readonly SubscriptionService           _service;

    private static readonly Guid     TestAccountId = Guid.NewGuid();
    private static readonly DateTime UtcNow        = DateTime.UtcNow;

    public SubscriptionServiceTests()
    {
        _clockMock.Setup(c => c.UtcNow).Returns(UtcNow);
        _service = new SubscriptionService(
            _subscriptionRepoMock.Object,
            _planRepoMock.Object,
            _clockMock.Object);
    }

    // ── GetSubscriptionByAccountIdAsync ───────────────────────────────────────

    [Fact]
    public async Task GetSubscriptionByAccountIdAsync_ExistingSubscription_ReturnsResponse()
    {
        var sub = BuildActive(TestAccountId);
        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync(sub);

        var result = await _service.GetSubscriptionByAccountIdAsync(TestAccountId);

        result.AccountId.Should().Be(TestAccountId);
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetSubscriptionByAccountIdAsync_NoSubscription_ThrowsSubscriptionNotFoundException()
    {
        // openapi.yaml GET /subscriptions/me: 404 when no subscription exists for the account.
        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync((SubscriptionAggregate?)null);

        var act = async () => await _service.GetSubscriptionByAccountIdAsync(TestAccountId);
        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    // ── CreateSubscriptionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateSubscriptionAsync_ValidRequest_AddsSubscriptionAndReturnsResponse()
    {
        var plan    = BuildPlan("basic");
        var request = new CreateSubscriptionRequest("basic");

        _planRepoMock
            .Setup(r => r.FindByIdAsync("basic", default))
            .ReturnsAsync(plan);
        _subscriptionRepoMock
            .Setup(r => r.FindActiveOrPendingByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync((SubscriptionAggregate?)null);
        _subscriptionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SubscriptionAggregate>(), default))
            .Returns(Task.CompletedTask);

        var result = await _service.CreateSubscriptionAsync(TestAccountId, request);

        result.AccountId.Should().Be(TestAccountId);
        result.PlanId.Should().Be("basic");
        result.Status.Should().Be("PendingPayment");
        _subscriptionRepoMock.Verify(r => r.AddAsync(It.IsAny<SubscriptionAggregate>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_PlanNotFound_ThrowsPlanNotFoundException()
    {
        // openapi.yaml POST /subscriptions: 400 when planId doesn't match a known plan.
        _planRepoMock
            .Setup(r => r.FindByIdAsync("ghost", default))
            .ReturnsAsync((Plan?)null);

        var act = async () => await _service.CreateSubscriptionAsync(
            TestAccountId, new CreateSubscriptionRequest("ghost"));

        await act.Should().ThrowAsync<PlanNotFoundException>();
        _subscriptionRepoMock.Verify(r => r.AddAsync(It.IsAny<SubscriptionAggregate>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_AccountAlreadyHasActiveSubscription_ThrowsDuplicateSubscriptionException()
    {
        // openapi.yaml POST /subscriptions: 409 when account already has Active or PendingPayment subscription.
        var plan     = BuildPlan("basic");
        var existing = BuildActive(TestAccountId);

        _planRepoMock
            .Setup(r => r.FindByIdAsync("basic", default))
            .ReturnsAsync(plan);
        _subscriptionRepoMock
            .Setup(r => r.FindActiveOrPendingByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync(existing);

        var act = async () => await _service.CreateSubscriptionAsync(
            TestAccountId, new CreateSubscriptionRequest("basic"));

        await act.Should().ThrowAsync<DuplicateSubscriptionException>();
        _subscriptionRepoMock.Verify(r => r.AddAsync(It.IsAny<SubscriptionAggregate>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_AccountAlreadyHasPendingSubscription_ThrowsDuplicateSubscriptionException()
    {
        // The 409 guard covers both Active AND PendingPayment existing subscriptions.
        var plan    = BuildPlan("basic");
        var pending = BuildPending(TestAccountId);

        _planRepoMock
            .Setup(r => r.FindByIdAsync("basic", default))
            .ReturnsAsync(plan);
        _subscriptionRepoMock
            .Setup(r => r.FindActiveOrPendingByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync(pending);

        var act = async () => await _service.CreateSubscriptionAsync(
            TestAccountId, new CreateSubscriptionRequest("basic"));

        await act.Should().ThrowAsync<DuplicateSubscriptionException>();
    }

    // ── CancelSubscriptionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CancelSubscriptionAsync_ActiveSubscription_CancelsAndCallsUpdate()
    {
        var sub = BuildActive(TestAccountId);
        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync(sub);
        _subscriptionRepoMock
            .Setup(r => r.UpdateAsync(sub, default))
            .Returns(Task.CompletedTask);

        await _service.CancelSubscriptionAsync(TestAccountId);

        sub.IsCancelled.Should().BeTrue();
        _subscriptionRepoMock.Verify(r => r.UpdateAsync(sub, default), Times.Once);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_NoSubscription_ThrowsSubscriptionNotFoundException()
    {
        // openapi.yaml DELETE /subscriptions/me: 404 when no subscription exists.
        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync((SubscriptionAggregate?)null);

        var act = async () => await _service.CancelSubscriptionAsync(TestAccountId);
        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    // ── ChangePlanAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePlanAsync_ValidRequest_UpdatesPlanIdAndCallsUpdate()
    {
        var sub  = BuildActive(TestAccountId);
        var plan = BuildPlan("premium");

        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync(sub);
        _planRepoMock
            .Setup(r => r.FindByIdAsync("premium", default))
            .ReturnsAsync(plan);
        _subscriptionRepoMock
            .Setup(r => r.UpdateAsync(sub, default))
            .Returns(Task.CompletedTask);

        var result = await _service.ChangePlanAsync(TestAccountId, new ChangePlanRequest("premium"));

        result.PlanId.Should().Be("premium");
        _subscriptionRepoMock.Verify(r => r.UpdateAsync(sub, default), Times.Once);
    }

    [Fact]
    public async Task ChangePlanAsync_NoSubscription_ThrowsSubscriptionNotFoundException()
    {
        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync((SubscriptionAggregate?)null);

        var act = async () => await _service.ChangePlanAsync(
            TestAccountId, new ChangePlanRequest("premium"));

        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    [Fact]
    public async Task ChangePlanAsync_PlanNotFound_ThrowsPlanNotFoundException()
    {
        var sub = BuildActive(TestAccountId);

        _subscriptionRepoMock
            .Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
            .ReturnsAsync(sub);
        _planRepoMock
            .Setup(r => r.FindByIdAsync("ghost", default))
            .ReturnsAsync((Plan?)null);

        var act = async () => await _service.ChangePlanAsync(
            TestAccountId, new ChangePlanRequest("ghost"));

        await act.Should().ThrowAsync<PlanNotFoundException>();
        _subscriptionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<SubscriptionAggregate>(), default), Times.Never);
    }

    // ── ActivateSubscriptionAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ActivateSubscriptionAsync_PendingSubscription_ActivatesAndCallsUpdate()
    {
        // ADR 0003: the bridge endpoint and the Phase-4 consumer both call this method.
        var sub   = BuildPending(TestAccountId);
        var subId = sub.SubscriptionId;

        _subscriptionRepoMock
            .Setup(r => r.FindByIdAsync(subId, default))
            .ReturnsAsync(sub);
        _subscriptionRepoMock
            .Setup(r => r.UpdateAsync(sub, default))
            .Returns(Task.CompletedTask);

        await _service.ActivateSubscriptionAsync(subId);

        sub.IsActive.Should().BeTrue();
        _subscriptionRepoMock.Verify(r => r.UpdateAsync(sub, default), Times.Once);
    }

    [Fact]
    public async Task ActivateSubscriptionAsync_UnknownSubscriptionId_ThrowsSubscriptionNotFoundException()
    {
        var ghostId = Guid.NewGuid();
        _subscriptionRepoMock
            .Setup(r => r.FindByIdAsync(ghostId, default))
            .ReturnsAsync((SubscriptionAggregate?)null);

        var act = async () => await _service.ActivateSubscriptionAsync(ghostId);
        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubscriptionAggregate BuildPending(Guid accountId) =>
        new(Guid.NewGuid(), accountId, "basic", DateTime.UtcNow);

    private static SubscriptionAggregate BuildActive(Guid accountId)
    {
        var sub = BuildPending(accountId);
        sub.Activate();
        return sub;
    }

    private static Plan BuildPlan(string planId) =>
        new(planId, "Test Plan", new Money(9.99m), 1, new VideoQuality("HD"));
}
