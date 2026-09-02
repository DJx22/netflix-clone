using NSubstitute;
using Payment.Application.Abstractions;
using Payment.Application.DTOs;
using Payment.Application.Exceptions;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using PaymentAggregate = Payment.Domain.Aggregates.Payment;

namespace Payment.Tests.Application;

/// <summary>
/// Unit tests for <see cref="PaymentService"/>.
/// All dependencies are substituted so no I/O occurs.
/// </summary>
public sealed class PaymentServiceTests
{
    // ── Substitutes ───────────────────────────────────────────────────────────

    private readonly IPaymentRepository  _repository       = Substitute.For<IPaymentRepository>();
    private readonly IPaymentSimulator   _simulator        = Substitute.For<IPaymentSimulator>();
    private readonly IDateTimeProvider   _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IEventPublisher     _eventPublisher   = Substitute.For<IEventPublisher>();
    private readonly PaymentService      _service;

    public PaymentServiceTests()
    {
        _service = new PaymentService(
            _repository,
            _simulator,
            _dateTimeProvider,
            _eventPublisher);
    }

    // ── CreatePaymentAsync — success path ─────────────────────────────────────

    [Fact]
    public async Task CreatePaymentAsync_SimulatorSucceeds_ReturnsSucceededResponse()
    {
        // Arrange
        var processedAt = new DateTime(2025, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        _dateTimeProvider.UtcNow.Returns(processedAt);
        _simulator.Charge(Arg.Any<bool?>()).Returns(true);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", null);

        // Act
        var response = await _service.CreatePaymentAsync(request);

        // Assert
        Assert.Equal("Succeeded", response.Status);
    }

    [Fact]
    public async Task CreatePaymentAsync_SimulatorSucceeds_PublishesPaymentCompletedEvent()
    {
        // Arrange
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(Arg.Any<bool?>()).Returns(true);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);
        _eventPublisher.PublishPaymentCompletedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", null);

        // Act
        await _service.CreatePaymentAsync(request);

        // Assert — event must fire exactly once on success (spec: "Publishes PaymentCompleted to RabbitMQ on success")
        await _eventPublisher.Received(1).PublishPaymentCompletedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePaymentAsync_SimulatorSucceeds_PersistsPayment()
    {
        // Arrange
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(Arg.Any<bool?>()).Returns(true);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", null);

        // Act
        await _service.CreatePaymentAsync(request);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePaymentAsync_SimulatorSucceeds_ResponseCarriesCorrectAmountAndCurrency()
    {
        // Arrange
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(Arg.Any<bool?>()).Returns(true);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var subscriptionId = Guid.NewGuid();
        var request = new CreatePaymentRequest(subscriptionId, 19.99m, "EUR", null);

        // Act
        var response = await _service.CreatePaymentAsync(request);

        // Assert
        Assert.Equal(19.99m,        response.Amount);
        Assert.Equal("EUR",         response.Currency);
        Assert.Equal(subscriptionId, response.SubscriptionId);
    }

    // ── CreatePaymentAsync — failure path ─────────────────────────────────────

    [Fact]
    public async Task CreatePaymentAsync_SimulatorFails_ReturnsFailedResponse()
    {
        // Arrange
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(Arg.Any<bool?>()).Returns(false);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", SimulateFailure: true);

        // Act
        var response = await _service.CreatePaymentAsync(request);

        // Assert
        Assert.Equal("Failed", response.Status);
    }

    [Fact]
    public async Task CreatePaymentAsync_SimulatorFails_DoesNotPublishEvent()
    {
        // Arrange — a failed charge must NOT trigger subscription activation
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(Arg.Any<bool?>()).Returns(false);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", SimulateFailure: true);

        // Act
        await _service.CreatePaymentAsync(request);

        // Assert — event publisher must never be called on failure
        await _eventPublisher.DidNotReceive().PublishPaymentCompletedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePaymentAsync_SimulatorFails_StillPersistsPayment()
    {
        // Arrange — failed charges are still billing records
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(Arg.Any<bool?>()).Returns(false);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", SimulateFailure: true);

        // Act
        await _service.CreatePaymentAsync(request);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePaymentAsync_SimulateFailureTrue_SimulatorCalledWithTrue()
    {
        // Arrange
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _simulator.Charge(true).Returns(false);
        _repository.AddAsync(Arg.Any<PaymentAggregate>(), Arg.Any<CancellationToken>())
                   .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest(Guid.NewGuid(), 9.99m, "USD", SimulateFailure: true);

        // Act
        await _service.CreatePaymentAsync(request);

        // Assert — the simulateFailure flag flows through to the simulator unchanged
        _simulator.Received(1).Charge(true);
    }

    // ── GetPaymentByIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPaymentByIdAsync_PaymentExists_ReturnsMappedResponse()
    {
        // Arrange
        var paymentId      = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var processedAt    = new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Utc);

        var payment = BuildResolvedSucceededPayment(paymentId, subscriptionId, processedAt);

        _repository.FindByIdAsync(paymentId, Arg.Any<CancellationToken>())
                   .Returns(payment);

        // Act
        var response = await _service.GetPaymentByIdAsync(paymentId);

        // Assert
        Assert.Equal(paymentId,      response.PaymentId);
        Assert.Equal(subscriptionId, response.SubscriptionId);
        Assert.Equal("Succeeded",    response.Status);
        Assert.Equal(processedAt,    response.ProcessedAtUtc);
    }

    [Fact]
    public async Task GetPaymentByIdAsync_PaymentNotFound_ThrowsPaymentNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _repository.FindByIdAsync(unknownId, Arg.Any<CancellationToken>())
                   .Returns((PaymentAggregate?)null);

        // Act
        Func<Task> act = () => _service.GetPaymentByIdAsync(unknownId);

        // Assert
        await Assert.ThrowsAsync<PaymentNotFoundException>(act);
    }

    // ── GetPaymentsBySubscriptionIdAsync ─────────────────────────────────────

    [Fact]
    public async Task GetPaymentsBySubscriptionIdAsync_PaymentsExist_ReturnsMappedList()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var processedAt    = DateTime.UtcNow;

        var payments = new List<PaymentAggregate>
        {
            BuildResolvedSucceededPayment(Guid.NewGuid(), subscriptionId, processedAt),
            BuildResolvedFailedPayment(Guid.NewGuid(), subscriptionId, processedAt.AddSeconds(-30))
        };

        _repository.FindBySubscriptionIdAsync(subscriptionId, Arg.Any<CancellationToken>())
                   .Returns(payments.AsReadOnly());

        // Act
        var result = await _service.GetPaymentsBySubscriptionIdAsync(subscriptionId);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetPaymentsBySubscriptionIdAsync_NoPayments_ReturnsEmptyList()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        _repository.FindBySubscriptionIdAsync(subscriptionId, Arg.Any<CancellationToken>())
                   .Returns(Array.Empty<PaymentAggregate>());

        // Act
        var result = await _service.GetPaymentsBySubscriptionIdAsync(subscriptionId);

        // Assert
        Assert.Empty(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaymentAggregate BuildResolvedSucceededPayment(
        Guid paymentId, Guid subscriptionId, DateTime processedAt)
    {
        var p = new PaymentAggregate(paymentId, subscriptionId, 9.99m, "USD");
        p.RecordSuccess(processedAt);
        return p;
    }

    private static PaymentAggregate BuildResolvedFailedPayment(
        Guid paymentId, Guid subscriptionId, DateTime processedAt)
    {
        var p = new PaymentAggregate(paymentId, subscriptionId, 9.99m, "USD");
        p.RecordFailure(processedAt);
        return p;
    }
}
