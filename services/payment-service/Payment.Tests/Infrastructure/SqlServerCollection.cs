namespace Payment.Tests.Infrastructure;

/// <summary>
/// Declares the "SqlServer" xUnit collection.
/// Tests within this collection share the same test context class but NOT a container instance —
/// each <see cref="PaymentRepositoryTests"/> creates its own container via <c>IAsyncLifetime</c>.
/// The collection attribute exists solely to serialise integration tests and prevent
/// multiple containers from being started in parallel, which can exhaust Docker resources.
/// </summary>
[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection { }
