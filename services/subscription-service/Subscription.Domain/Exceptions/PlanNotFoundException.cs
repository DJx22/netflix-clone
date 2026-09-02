namespace Subscription.Domain.Exceptions;

/// <summary>
/// Thrown when a requested plan ID does not correspond to any known plan.
/// Maps to HTTP 404 or 400 at the API boundary depending on whether the plan
/// was supplied by the caller or referenced internally.
/// </summary>
public sealed class PlanNotFoundException : DomainException
{
    /// <summary>
    /// Initialises a <see cref="PlanNotFoundException"/>.
    /// </summary>
    /// <param name="planId">The unrecognised plan identifier.</param>
    public PlanNotFoundException(string planId)
        : base($"Plan '{planId}' was not found.") { }
}
