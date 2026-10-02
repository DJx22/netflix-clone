using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Subscription.Infrastructure.Persistence;

namespace Subscription.Infrastructure.Migrations;

[DbContext(typeof(SubscriptionDbContext))]
[Migration("20261002161700_SeedDefaultPlans")]
public partial class SeedDefaultPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO [Plans] ([PlanId], [Name], [PriceMonthly], [MaxProfiles], [VideoQuality])
            SELECT seed.[PlanId], seed.[Name], seed.[PriceMonthly], seed.[MaxProfiles], seed.[VideoQuality]
            FROM (VALUES
                ('basic', 'Basic', 9.99, 1, 'SD'),
                ('standard', 'Standard', 14.99, 2, 'HD'),
                ('premium', 'Premium', 19.99, 4, 'UHD')
            ) AS seed ([PlanId], [Name], [PriceMonthly], [MaxProfiles], [VideoQuality])
            WHERE NOT EXISTS (
                SELECT 1 FROM [Plans] existing WHERE existing.[PlanId] = seed.[PlanId]
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [Plans]
            WHERE [PlanId] IN ('basic', 'standard', 'premium');
            """);
    }
}
