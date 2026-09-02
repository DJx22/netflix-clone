using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subscription.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by `dotnet ef migrations` tooling.
/// Never instantiated at runtime — the DI container builds the real context.
/// Connection string here is a placeholder; migrations are applied against a
/// real database via pipeline, not by hand (§9).
/// </summary>
internal sealed class SubscriptionDbContextFactory : IDesignTimeDbContextFactory<SubscriptionDbContext>
{
    public SubscriptionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SubscriptionDbContext>()
            .UseSqlServer("Server=localhost;Database=SubscriptionDb;TrustServerCertificate=True;Trusted_Connection=True;")
            .Options;

        return new SubscriptionDbContext(options);
    }
}
