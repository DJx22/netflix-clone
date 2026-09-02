using PaymentAggregate = Payment.Domain.Aggregates.Payment;

using Microsoft.EntityFrameworkCore;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPaymentRepository"/>.
/// </summary>
/// <remarks>
/// Lifetime: <b>Scoped</b> — holds a scoped <see cref="PaymentDbContext"/> (§7).
/// <para>
/// ADR 0004: entities are returned as tracked objects so that SaveChangesAsync can
/// detect any changes. In practice Payment aggregates are write-once (immutable after
/// outcome is recorded), so tracking adds no overhead risk here.
/// </para>
/// </remarks>
public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;

    /// <summary>Initialises a new <see cref="PaymentRepository"/>.</summary>
    public PaymentRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<PaymentAggregate?> FindByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PaymentAggregate>> FindBySubscriptionIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        // Ordered most-recent first to match the spec's implied "payment history" display order.
        return await _dbContext.Payments
            .Where(p => p.SubscriptionId == subscriptionId)
            .OrderByDescending(p => p.ProcessedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(
        PaymentAggregate payment,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Payments
            .AddAsync(payment, cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
