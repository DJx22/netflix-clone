using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Payment.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by <c>dotnet ef migrations</c> tooling.
/// Never instantiated at runtime — the DI container builds the real context.
/// </summary>
/// <remarks>
/// Connection string is a local-dev placeholder. Migrations are applied against a
/// real database via a pipeline step using the runtime connection string injected
/// through environment variables — never by hand against a shared environment (§9).
/// </remarks>
internal sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    /// <inheritdoc/>
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=localhost;Database=PaymentDb;TrustServerCertificate=True;Trusted_Connection=True;")
            .Options;

        return new PaymentDbContext(options);
    }
}
