namespace Payment.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the <c>RabbitMq</c> configuration section in
/// <c>Payment.Infrastructure</c>.
/// </summary>
/// <remarks>
/// <para>
/// Bind via <c>services.Configure&lt;RabbitMqOptions&gt;(configuration.GetSection("RabbitMq"))</c>
/// in <see cref="DependencyInjection.AddInfrastructure"/>.
/// </para>
/// <para>
/// Docker Compose injects these as env vars with the double-underscore separator:
/// <c>RabbitMq__Host=rabbitmq</c>, <c>RabbitMq__Port=5672</c>, etc.
/// Local dev defaults (appsettings.local.json) point at <c>localhost:5672</c>.
/// </para>
/// </remarks>
public sealed class RabbitMqOptions
{
    /// <summary>Hostname or IP of the RabbitMQ broker. Default: <c>localhost</c>.</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>AMQP port. Default: <c>5672</c>.</summary>
    public int Port { get; init; } = 5672;

    /// <summary>Broker username. Default: <c>guest</c>.</summary>
    public string UserName { get; init; } = "guest";

    /// <summary>Broker password. Default: <c>guest</c>.</summary>
    public string Password { get; init; } = "guest";

    /// <summary>
    /// Name of the durable direct exchange that carries payment domain events.
    /// Must match the binding declared by the Subscription consumer.
    /// Default: <c>payment.events</c> (W1.4 contract).
    /// </summary>
    public string ExchangeName { get; init; } = "payment.events";
}
