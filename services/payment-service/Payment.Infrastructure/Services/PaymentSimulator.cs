using Payment.Application.Abstractions;

namespace Payment.Infrastructure.Services;

/// <summary>
/// In-process mock payment charge simulator.
/// </summary>
/// <remarks>
/// The spec describes an "in-process simulator so the service can be tested without
/// a network dependency" (openapi.yaml info.description). This is that simulator.
/// Registered as <b>Singleton</b> — holds no mutable state; <see cref="_random"/> is
/// the only field and <see cref="Random"/> is thread-safe for reads after construction (§7).
/// </remarks>
public sealed class PaymentSimulator : IPaymentSimulator
{
    // A single shared Random instance is safe for Singleton registration:
    // Random.Shared (net6+) is thread-safe. Using Random.Shared avoids the
    // seed-collision risk of constructing multiple instances close together.
    private static readonly Random _random = Random.Shared;

    /// <inheritdoc/>
    public bool Charge(bool? simulateFailure)
    {
        // simulateFailure=true  → always fail  (deterministic test path)
        // simulateFailure=false → always succeed (deterministic test path)
        // simulateFailure=null  → random outcome (spec default when omitted)
        if (simulateFailure.HasValue)
        {
            return !simulateFailure.Value;
        }

        // ~80% success rate matches a realistic mock gateway without being so high
        // that the failure path is hard to trigger manually.
        return _random.NextDouble() >= 0.2;
    }
}
