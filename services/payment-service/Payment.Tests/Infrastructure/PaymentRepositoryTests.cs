using Microsoft.EntityFrameworkCore;
using Payment.Domain.Enums;
using Payment.Infrastructure.Persistence;
using Payment.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
using PaymentAggregate = Payment.Domain.Aggregates.Payment;

namespace Payment.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="PaymentRepository"/> against a real SQL Server container.
/// Each test gets a freshly migrated database — no shared mutable state between tests.
/// </summary>
/// <remarks>
/// These tests require Docker to be running. If Docker is unavailable, Testcontainers will
/// throw and all tests in this class will fail. This is intentional: §14 of the coding standard
/// forbids mocking the repository in Infrastructure tests — the entire point is to prove the
/// real EF Core + SQL Server code works.
///
/// ADR 0001: SQL Server is the required provider. A different engine (SQLite, InMemory) would
/// not exercise the same type mappings or the <c>HasConversion</c> on <c>ProcessedAtUtc</c>.
/// </remarks>
[Collection("SqlServer")]
public sealed class PaymentRepositoryTests : IAsyncLifetime
{
    // ── Container lifecycle ───────────────────────────────────────────────────

    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private PaymentDbContext  _dbContext  = default!;
    private PaymentRepository _repository = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _dbContext = new PaymentDbContext(options);

        // Apply all EF Core migrations — same path as production (ADR 0001).
        await _dbContext.Database.MigrateAsync();

        _repository = new PaymentRepository(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ── AddAsync + FindByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenFindByIdAsync_RoundTripsPaymentSuccessfully()
    {
        // Arrange
        var payment = BuildSucceededPayment();

        // Act
        await _repository.AddAsync(payment);
        var retrieved = await _repository.FindByIdAsync(payment.PaymentId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(payment.PaymentId,      retrieved.PaymentId);
        Assert.Equal(payment.SubscriptionId, retrieved.SubscriptionId);
        Assert.Equal(payment.Amount,         retrieved.Amount);
        Assert.Equal(payment.Currency,       retrieved.Currency);
        Assert.Equal(PaymentStatus.Succeeded, retrieved.Status);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var result = await _repository.FindByIdAsync(unknownId);

        // Assert
        Assert.Null(result);
    }

    // ── FindBySubscriptionIdAsync ─────────────────────────────────────────────

    [Fact]
    public async Task FindBySubscriptionIdAsync_MultiplePayments_ReturnsAllForThatSubscription()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var otherSubId     = Guid.NewGuid();

        var payment1 = BuildSucceededPayment(subscriptionId);
        var payment2 = BuildFailedPayment(subscriptionId);
        var noise    = BuildSucceededPayment(otherSubId); // must not appear in results

        await _repository.AddAsync(payment1);
        await _repository.AddAsync(payment2);
        await _repository.AddAsync(noise);

        // Act
        var results = await _repository.FindBySubscriptionIdAsync(subscriptionId);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(subscriptionId, r.SubscriptionId));
    }

    [Fact]
    public async Task FindBySubscriptionIdAsync_NoMatchingPayments_ReturnsEmptyList()
    {
        // Arrange
        var noPaymentsSubId = Guid.NewGuid();

        // Act
        var results = await _repository.FindBySubscriptionIdAsync(noPaymentsSubId);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task FindBySubscriptionIdAsync_ReturnsPaymentsOrderedMostRecentFirst()
    {
        // Arrange — older payment added first, newer second
        var subscriptionId = Guid.NewGuid();
        var older = DateTime.UtcNow.AddMinutes(-10);
        var newer = DateTime.UtcNow;

        var olderPayment = BuildSucceededPayment(subscriptionId, processedAt: older);
        var newerPayment = BuildSucceededPayment(subscriptionId, processedAt: newer);

        await _repository.AddAsync(olderPayment);
        await _repository.AddAsync(newerPayment);

        // Act
        var results = await _repository.FindBySubscriptionIdAsync(subscriptionId);

        // Assert — most-recent first (OrderByDescending in repository implementation)
        Assert.Equal(newerPayment.PaymentId, results[0].PaymentId);
        Assert.Equal(olderPayment.PaymentId, results[1].PaymentId);
    }

    // ── Type mapping: DateTime.Kind and Status ────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenFindByIdAsync_ProcessedAtUtcHasKindUtc()
    {
        // Arrange — SQL Server stores datetime2 without Kind; the EF value converter re-asserts UTC
        var payment = BuildSucceededPayment();

        // Act
        await _repository.AddAsync(payment);
        var retrieved = await _repository.FindByIdAsync(payment.PaymentId);

        // Assert — PaymentConfiguration applies DateTime.SpecifyKind(read, DateTimeKind.Utc)
        Assert.NotNull(retrieved);
        Assert.Equal(DateTimeKind.Utc, retrieved.ProcessedAtUtc.Kind);
    }

    [Fact]
    public async Task AddAsync_ThenFindByIdAsync_StatusPersistedAsStringNotInteger()
    {
        // Arrange
        var payment = BuildSucceededPayment();
        await _repository.AddAsync(payment);

        // Act — query the raw column via Dapper-style direct SQL to verify storage format
        // (ADR 0004: "Store enum name, not its integer ordinal")
        var rawStatus = await _dbContext.Database
            .SqlQueryRaw<string>("SELECT TOP 1 Status FROM Payments WHERE PaymentId = {0}", payment.PaymentId)
            .FirstOrDefaultAsync();

        // Assert — stored as "Succeeded", not "1"
        Assert.Equal("Succeeded", rawStatus);
    }

    [Fact]
    public async Task AddAsync_FailedPayment_StatusPersistedAsFailed()
    {
        // Arrange
        var payment = BuildFailedPayment();
        await _repository.AddAsync(payment);

        // Act
        var retrieved = await _repository.FindByIdAsync(payment.PaymentId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(PaymentStatus.Failed, retrieved.Status);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaymentAggregate BuildSucceededPayment(
        Guid? subscriptionId = null,
        DateTime? processedAt = null)
    {
        var payment = new PaymentAggregate(
            Guid.NewGuid(),
            subscriptionId ?? Guid.NewGuid(),
            9.99m,
            "USD");
        payment.RecordSuccess(processedAt ?? DateTime.UtcNow);
        return payment;
    }

    private static PaymentAggregate BuildFailedPayment(
        Guid? subscriptionId = null,
        DateTime? processedAt = null)
    {
        var payment = new PaymentAggregate(
            Guid.NewGuid(),
            subscriptionId ?? Guid.NewGuid(),
            9.99m,
            "USD");
        payment.RecordFailure(processedAt ?? DateTime.UtcNow);
        return payment;
    }
}
