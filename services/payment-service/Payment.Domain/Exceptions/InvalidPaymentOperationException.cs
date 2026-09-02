namespace Payment.Domain.Exceptions;

/// <summary>
/// Thrown when a state-transition is attempted on a <see cref="Aggregates.Payment"/>
/// that the aggregate's current status does not permit.
/// </summary>
public sealed class InvalidPaymentOperationException : DomainException
{
    /// <param name="message">Human-readable description of the violated rule.</param>
    public InvalidPaymentOperationException(string message) : base(message) { }
}
