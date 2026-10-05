using ClubStack.Application.Authorization;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class D1ReferenceDataTests : DatabaseTestBase
{
    public D1ReferenceDataTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Seed_provides_complete_permission_catalogue()
    {
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
        await seeder.SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var storedKeys = await context.Permissions.AsNoTracking().Select(p => p.Key).ToListAsync();

        Assert.Equal(Permissions.Catalog.Count, storedKeys.Count);
        Assert.Equal(Permissions.Catalog.Select(c => c.Key).OrderBy(k => k).ToArray(), storedKeys.OrderBy(k => k).ToArray());
        Assert.Equal(storedKeys.Count, storedKeys.Distinct().Count());
        Assert.Equal(Permissions.Catalog.Count(c => c.IsSensitive), await context.Permissions.CountAsync(p => p.IsSensitive));
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        Assert.Equal(Permissions.Catalog.Count, await context.Permissions.CountAsync());
    }

    [Fact]
    public async Task Seed_creates_all_baseline_roles_as_system_roles()
    {
        var expectedRoleNames = new[]
        {
            "System Administrator", "Club Manager", "Membership Officer", "Finance Officer", "Cashier",
            "POS Operator", "Golf Operations", "Tournament Manager", "Inventory Officer",
            "Procurement Officer", "F&B Manager", "F&B Operator", "Pro Shop Operator", "Asset Manager",
            "Maintenance Officer", "Reception Officer", "Event Manager", "Auditor", "Report Viewer",
        };

        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
        await seeder.SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();
        var roles = await context.Roles.AsNoTracking().ToListAsync();

        foreach (var expected in expectedRoleNames)
        {
            Assert.Single(roles, r => r.Name == expected);
            Assert.True(roles.Single(r => r.Name == expected).IsSystem);
        }
    }

    [Fact]
    public async Task Seed_provides_starter_payment_methods_and_default_currency()
    {
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
        await seeder.SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<ClubStackDbContext>();

        var methods = await context.PaymentMethods.AsNoTracking().Select(p => p.Code).ToListAsync();
        Assert.Contains("CASH", methods);
        Assert.Contains("CARD", methods);
        Assert.Contains("ACCOUNT", methods);

        var defaultCurrency = await context.Currencies.AsNoTracking().SingleAsync(c => c.IsDefault);
        Assert.Equal("ZAR", defaultCurrency.Code);
    }
}