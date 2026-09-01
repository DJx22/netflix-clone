using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by `dotnet ef migrations` tooling.
/// Never instantiated at runtime — the DI container builds the real context.
/// Connection string here is a placeholder; migrations are applied against a
/// real database via pipeline, not by hand (§9).
/// </summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=IdentityDb;TrustServerCertificate=True;Trusted_Connection=True;")
            .Options;

        return new IdentityDbContext(options);
    }
}
