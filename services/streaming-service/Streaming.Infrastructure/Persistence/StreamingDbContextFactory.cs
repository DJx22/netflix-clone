using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Streaming.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by <c>dotnet ef migrations</c> tooling.
/// Never instantiated at runtime — the DI container builds the real context.
/// Connection string here is a placeholder; migrations are applied against a
/// real database via pipeline, not by hand (§9).
/// </summary>
internal sealed class StreamingDbContextFactory : IDesignTimeDbContextFactory<StreamingDbContext>
{
    /// <inheritdoc/>
    public StreamingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StreamingDbContext>()
            .UseSqlServer("Server=localhost;Database=StreamingDb;TrustServerCertificate=True;Trusted_Connection=True;")
            .Options;

        return new StreamingDbContext(options);
    }
}
