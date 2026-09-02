using Subscription.Domain.Exceptions;

namespace Subscription.Domain.Exceptions;

/// <summary>
/// Thrown when an attempt is made to create a subscription for an account
/// that already has one in <c>Active</c> or <c>PendingPayment</c> status.
/// Maps to HTTP 409 at the API boundary.
/// </summary>
public sealed class DuplicateSubscriptionException : DomainException
{
    /// <summary>
    /// Initialises a <see cref="DuplicateSubscriptionException"/>.
    /// </summary>
    /// <param name="accountId">The account for which the duplicate was detected.</param>
    public DuplicateSubscriptionException(Guid accountId)
        : base($"Account '{accountId}' already has an active or pending subscription.") { }
}
