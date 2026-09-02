namespace Payment.Application.Abstractions;

/// <summary>
/// Simulates a payment charge attempt and returns whether it succeeded.
/// </summary>
/// <remarks>
/// The spec describes an "in-process simulator" so the service can be tested without
/// a network dependency (openapi.yaml info.description). This interface isolates that
/// simulation from the application service, keeping the service testable with a
/// controlled outcome via a mock implementation.
/// Infrastructure implements this with the randomised logic; tests inject a stub.
/// </remarks>
public interface IPaymentSimulator
{
    /// <summary>
    /// Executes the mock charge and returns <see langword="true"/> if it succeeded,
    /// <see langword="false"/> if it failed.
    /// </summary>
    /// <param name="simulateFailure">
    /// Testing-only override. When <see langword="true"/>, always returns <see langword="false"/>.
    /// When <see langword="null"/>, the outcome is randomised (spec default).
    /// </param>
    bool Charge(bool? simulateFailure);
}
