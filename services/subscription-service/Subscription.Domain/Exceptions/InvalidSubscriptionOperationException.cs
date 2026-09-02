namespace Subscription.Domain.Exceptions;

/// <summary>
/// Thrown when a state-transition method is called on a subscription whose current
/// status does not permit that operation (e.g. cancelling a <c>PendingPayment</c>
/// subscription, or changing the plan of a <c>Cancelled</c> one).
/// </summary>
public sealed class InvalidSubscriptionOperationException : DomainException
{
    /// <summary>
    /// Initialises a <see cref="InvalidSubscriptionOperationException"/>.
    /// </summary>
    /// <param name="message">Description of the invalid operation and the current status.</param>
    public InvalidSubscriptionOperationException(string message) : base(message) { }
}
