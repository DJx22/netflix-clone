using Subscription.Domain.ValueObjects;

namespace Subscription.Domain.Entities;

/// <summary>
/// A subscription plan offered to accounts (e.g. Basic, Standard, Premium).
/// <para>
/// <c>Plan</c> is a reference entity owned by the Subscription bounded context.
/// It is not the aggregate root — <see cref="Aggregates.Subscription"/> is. Plans
/// are read by the Application layer to validate that a requested <c>planId</c>
/// exists before creating or changing a subscription.
/// </para>
/// </summary>
public sealed class Plan
{
    /// <summary>Gets the unique plan identifier (string key, not a GUID — see OpenAPI spec).</summary>
    public string PlanId { get; }

    /// <summary>Gets the human-readable name of the plan.</summary>
    public string Name { get; }

    /// <summary>Gets the monthly price for the plan.</summary>
    public Money PriceMonthly { get; }

    /// <summary>Gets the maximum number of simultaneous viewer profiles permitted.</summary>
    public int MaxProfiles { get; }

    /// <summary>Gets the video quality tier (e.g. "SD", "HD", "4K").</summary>
    public VideoQuality VideoQuality { get; }

    /// <summary>Gets the optional human-readable description of the plan.</summary>
    public string? Description { get; }

    /// <summary>
    /// Initialises a <see cref="Plan"/> instance.
    /// </summary>
    /// <param name="planId">Must be non-null and non-whitespace.</param>
    /// <param name="name">Must be non-null and non-whitespace.</param>
    /// <param name="priceMonthly">Must be non-negative (enforced by <see cref="Money"/>).</param>
    /// <param name="maxProfiles">Must be at least 1 — a plan with zero profiles is unusable.</param>
    /// <param name="videoQuality">Must be non-null and non-whitespace.</param>
    /// <param name="description">Optional marketing description; may be null.</param>
    public Plan(
        string planId,
        string name,
        Money priceMonthly,
        int maxProfiles,
        VideoQuality videoQuality,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(planId))
        {
            throw new ArgumentException("Plan ID must not be empty.", nameof(planId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plan name must not be empty.", nameof(name));
        }

        // A plan that allows zero profiles cannot be used — enforce at construction rather
        // than discovering the problem at entitlement-check time.
        if (maxProfiles < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxProfiles), "A plan must allow at least one profile.");
        }

        PlanId = planId;
        Name = name;
        PriceMonthly = priceMonthly;
        MaxProfiles = maxProfiles;
        VideoQuality = videoQuality;
        Description = description;
    }
}
