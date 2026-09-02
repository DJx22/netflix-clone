namespace Subscription.Application.Exceptions;

/// <summary>
/// Thrown when a repository query returns no subscription for the requested account
/// or subscription ID. Maps to HTTP 404 at the API boundary.
/// <para>
/// This is an Application-level concern, not a domain rule violation — the domain
/// models what a subscription <em>is</em>; the application layer handles the case
/// where one does not yet exist for a given query.
/// </para>
/// </summary>
public sealed class SubscriptionNotFoundException : Exception
{
    private SubscriptionNotFoundException(string message) : base(message) { }

    /// <summary>
    /// Creates an exception indicating the account has no subscription.
    /// Used by GET, DELETE, and PUT plan operations.
    /// </summary>
    public static SubscriptionNotFoundException ForAccount(Guid accountId) =>
        new($"No subscription found for account '{accountId}'.");

    /// <summary>
    /// Creates an exception indicating a subscription with the given ID does not exist.
    /// Used by the PaymentCompleted event handler when activating a subscription.
    /// </summary>
    public static SubscriptionNotFoundException ForSubscriptionId(Guid subscriptionId) =>
        new($"No subscription found with ID '{subscriptionId}'.");
}
