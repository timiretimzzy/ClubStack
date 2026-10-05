using ClubStack.Application.Authorization;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Identity;
using ClubStack.Application.Platform;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class D1PlatformAndIdentityTests : DatabaseTestBase
{
    private const string StrongPassword = "Str0ng!Pass12";

    public D1PlatformAndIdentityTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();
    }

    /// <summary>Unique code that fits short (e.g. varchar(8)) columns.</summary>
    private static string ShortCode(string tag) => $"{tag}{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    private static ClubInput ClubInput(string code) => new(
        $"D1 Club {code}", code, "1 Main Rd", string.Empty, "Johannesburg", "Gauteng",
        "2001", "South Africa", "0110000000", $"club-{code}@example.com", IsActive: true);

    // ---------- Platform configuration store ----------

    [Fact]
    public async Task Club_create_update_and_conflict()
    {
        var code = UniquePrefix("CLB");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();

            var created = await store.CreateClubAsync(ClubInput(code));
            Assert.Equal(code, created.Code);
            Assert.True(created.IsActive);

            var fetched = await store.GetClubAsync(created.Id);
            Assert.Equal(created.Name, fetched.Name);

            var updated = await store.UpdateClubAsync(created.Id, ClubInput(code) with { Name = $"{code} Renamed" });
            Assert.Equal($"{code} Renamed", updated.Name);

            await Assert.ThrowsAsync<ConflictException>(() => store.CreateClubAsync(ClubInput(code)));
            await Assert.ThrowsAsync<NotFoundException>(() => store.GetClubAsync(Guid.NewGuid()));
        }
    }

    [Fact]
    public async Task GetPrimaryClubAsync_returns_first_created_club()
    {
        var firstCode = UniquePrefix("PRI");
        var secondCode = UniquePrefix("PRI");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
            var first = await store.CreateClubAsync(ClubInput(firstCode));
            var second = await store.CreateClubAsync(ClubInput(secondCode));

            var primary = await store.GetPrimaryClubAsync();
            Assert.NotNull(primary);
            Assert.Equal(first.Id, primary.Id);
            Assert.NotEqual(second.Id, primary.Id);
        }
    }

    [Fact]
    public async Task Department_create_requires_club_and_unique_code_per_club()
    {
        var clubCode = UniquePrefix("CLB");
        var depCode = UniquePrefix("DEP");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
            var club = await store.CreateClubAsync(ClubInput(clubCode));

            var department = await store.CreateDepartmentAsync(club.Id, new DepartmentInput($"{depCode} Dept", depCode, IsActive: true));
            Assert.Equal(club.Id, department.ClubId);

            await Assert.ThrowsAsync<ConflictException>(
                () => store.CreateDepartmentAsync(club.Id, new DepartmentInput($"{depCode} Dept 2", depCode, IsActive: true)));
            await Assert.ThrowsAsync<NotFoundException>(
                () => store.CreateDepartmentAsync(Guid.NewGuid(), new DepartmentInput($"{depCode} Dept 3", depCode, IsActive: true)));
        }
    }

    [Fact]
    public async Task Currency_default_can_only_exist_once()
    {
        await SeedAsync(Services);
        var code = ShortCode("CUR");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
            var currency = await store.CreateCurrencyAsync(new CurrencyInput(code, $"{code} Test", "R", 2, IsDefault: false, IsActive: true));
            Assert.NotNull(currency);

            await Assert.ThrowsAsync<ConflictException>(() =>
                store.CreateCurrencyAsync(new CurrencyInput(code, $"{code} Test 2", "R", 2, IsDefault: false, IsActive: true)));
            await Assert.ThrowsAsync<ConflictException>(() =>
                store.CreateCurrencyAsync(new CurrencyInput(ShortCode("CUR"), "Default", "R", 2, IsDefault: true, IsActive: true)));
        }
    }

    [Fact]
    public async Task Numbering_sequence_computes_example_and_rejects_duplicate_prefix()
    {
        var prefix = UniquePrefix("NV");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
            var created = await store.CreateNumberingSequenceAsync(new NumberingSequenceInput(prefix, 5, 42));
            Assert.Equal($"{prefix}00042", created.Example);

            await Assert.ThrowsAsync<ConflictException>(
                () => store.CreateNumberingSequenceAsync(new NumberingSequenceInput(prefix, 5, 1)));
        }
    }

    [Fact]
    public async Task System_configuration_upserts_and_protects_system_keys()
    {
        var key = UniquePrefix("CFG");

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
            var set = await store.SetSystemConfigurationAsync(
                new SystemConfigurationInput(key, "alpha", "String", "D1 test configuration"), "user-1");
            Assert.Equal("alpha", set.Value);
            Assert.False(set.IsSystem);

            var read = await store.GetSystemConfigurationAsync(key);
            Assert.Equal("alpha", read.Value);

            var updated = await store.SetSystemConfigurationAsync(
                new SystemConfigurationInput(key, "beta", "String", "D1 test configuration"), "user-2");
            Assert.Equal("beta", updated.Value);
            Assert.Equal("user-2", updated.UpdatedByUserId);
        }
    }

    [Fact]
    public async Task System_configuration_refuses_overwriting_system_protected_keys()
    {
        var key = UniquePrefix("SYS");
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();

        context.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Key = key,
            Value = "seed",
            ValueType = "String",
            Description = "System protected",
            IsSystem = true,
            UpdatedByUserId = "system",
        });
        await context.SaveChangesAsync();

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPlatformConfigurationStore>();
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                store.SetSystemConfigurationAsync(new SystemConfigurationInput(key, "changed", "String", "System protected"), "user-1"));
        }
    }

    // ---------- Identity administration ----------

    private static CreateUserInput CreateUser(string userName, string password = StrongPassword) => new(
        userName, $"{userName}@example.com", "Sipho", "D1 Tester", "0820000000", password, ["Report Viewer"]);

    [Fact]
    public async Task User_create_assigns_roles_and_duplicate_username_conflicts()
    {
        await SeedAsync(Services);
        var userName = UniquePrefix("usr");

        await using (var scope = Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();

            var created = await identity.CreateUserAsync(CreateUser(userName));
            Assert.True(created.IsActive);
            Assert.Equal(["Report Viewer"], created.Roles);

            var fetched = await identity.GetUserAsync(created.Id);
            Assert.Equal(userName, fetched.UserName);

            await Assert.ThrowsAsync<ValidationException>(() => identity.CreateUserAsync(CreateUser(userName)));
            await Assert.ThrowsAsync<ValidationException>(() =>
                identity.CreateUserAsync(CreateUser(UniquePrefix("usr")) with { Password = "weak" }));
            await Assert.ThrowsAsync<ValidationException>(() =>
                identity.CreateUserAsync(CreateUser(UniquePrefix("usr")) with { Roles = ["DoesNotExist"] }));
        }
    }

    [Fact]
    public async Task Role_permission_assignments_validate_catalogue_and_roles_are_protected()
    {
        await SeedAsync(Services);
        var roleName = $"D1Role{UniquePrefix("RL")}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();

            var created = await identity.CreateRoleAsync(roleName, [Permissions.PlatformConfigurationView, Permissions.IdentityPermissionView]);
            Assert.False(created.IsSystem);
            Assert.Equal(2, created.Permissions.Count);

            var updated = await identity.UpdateRolePermissionsAsync(created.Id, [Permissions.AuditView]);
            Assert.Equal([Permissions.AuditView], updated.Permissions);

            await Assert.ThrowsAsync<ValidationException>(
                () => identity.UpdateRolePermissionsAsync(created.Id, ["Not.A.Real.Permission"]));
            await Assert.ThrowsAsync<ConflictException>(() => identity.CreateRoleAsync(roleName, []));

            var user = await identity.CreateUserAsync(CreateUser(UniquePrefix("usr")) with { Roles = [roleName] });
            Assert.Equal([roleName], user.Roles);
            await Assert.ThrowsAsync<ConflictException>(() => identity.DeleteRoleAsync(created.Id));
        }
    }

    [Fact]
    public async Task System_role_cannot_be_deleted()
    {
        await SeedAsync(Services);

        await using (var scope = Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
            var systemRole = (await identity.GetRolesAsync()).Single(r => r.Name == "Report Viewer");
            await Assert.ThrowsAsync<ConflictException>(() => identity.DeleteRoleAsync(systemRole.Id));
        }
    }

    [Fact]
    public async Task Deactivate_user_revokes_all_sessions()
    {
        await SeedAsync(Services);
        var userName = UniquePrefix("usr");
        var userId = await CreateUserWithSessionAsync(userName);

        await using (var scope = Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
            await identity.DeactivateUserAsync(userId, "Test deactivation");

            var sessionStore = scope.ServiceProvider.GetRequiredService<IUserSessionStore>();
            var after = await sessionStore.ListAsync(userId);
            Assert.Single(after);
            Assert.False(after[0].IsActive);
            Assert.NotNull(after[0].RevokedAtUtc);
            Assert.Equal("Test deactivation", after[0].RevocationReason);

            var user = await identity.GetUserAsync(userId);
            Assert.False(user.IsActive);
        }
    }

    [Fact]
    public async Task Reset_password_revokes_all_sessions()
    {
        await SeedAsync(Services);
        var userName = UniquePrefix("usr");
        var userId = await CreateUserWithSessionAsync(userName);

        await using (var scope = Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
            await identity.ResetPasswordAsync(userId, "N3w!Str0ngPass123");

            var sessionStore = scope.ServiceProvider.GetRequiredService<IUserSessionStore>();
            var after = await sessionStore.ListAsync(userId);
            Assert.Single(after);
            Assert.False(after[0].IsActive);
            Assert.Contains("revoked", after[0].RevocationReason, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<Guid> CreateUserWithSessionAsync(string userName)
    {
        await using var scope = Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var user = await identity.CreateUserAsync(CreateUser(userName));

        var sessionStore = scope.ServiceProvider.GetRequiredService<IUserSessionStore>();
        var sessionId = Guid.CreateVersion7();
        var issued = DateTimeOffset.UtcNow;
        await sessionStore.CreateAsync(sessionId, user.Id, user.UserName, issued, issued.AddHours(12));

        var sessions = await sessionStore.ListAsync(user.Id);
        Assert.Single(sessions);
        Assert.True(sessions[0].IsActive);

        return user.Id;
    }

    [Fact]
    public async Task Session_store_touches_and_revokes()
    {
        await SeedAsync(Services);
        var userName = UniquePrefix("usr");

        using var scope = Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var user = await identity.CreateUserAsync(CreateUser(userName));

        var sessionStore = scope.ServiceProvider.GetRequiredService<IUserSessionStore>();
        var sessionId = Guid.CreateVersion7();
        var issued = DateTimeOffset.UtcNow;
        await sessionStore.CreateAsync(sessionId, user.Id, user.UserName, issued, issued.AddHours(12));

        var touched = issued.AddMinutes(5);
        await sessionStore.TouchAsync(sessionId, touched);
        var afterTouch = await sessionStore.GetAsync(sessionId);
        // Postgres stores timestamptz with microsecond precision.
        Assert.True(Math.Abs((afterTouch!.LastSeenAtUtc - touched).TotalMilliseconds) < 0.01);

        await sessionStore.RevokeAsync(sessionId, "Logged out", null);
        var revoked = await sessionStore.GetAsync(sessionId);
        Assert.False(revoked!.IsActive);
        Assert.Equal("Logged out", revoked.RevocationReason);

        await sessionStore.RevokeAsync(sessionId, "Again", null);
        Assert.Equal("Logged out", (await sessionStore.GetAsync(sessionId))!.RevocationReason);
    }

    [Fact]
    public async Task Claims_factory_emits_stored_role_permissions()
    {
        await SeedAsync(Services);
        var roleName = $"D1Claims{UniquePrefix("RL")}";
        var userName = UniquePrefix("usr");

        await using (var scope = Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
            var role = await identity.CreateRoleAsync(roleName, [Permissions.AuditView, Permissions.PlatformConfigurationView]);
            var user = await identity.CreateUserAsync(CreateUser(userName) with { Roles = [roleName] });

            var principals = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ClubStackUser>>();
            var principal = await principals.CreateAsync(await TestHelpers.FindUserAsync(scope.ServiceProvider, user.Id));

            var permissionClaims = principal.Claims
                .Where(c => c.Type == PermissionClaimTypes.Permission)
                .Select(c => c.Value)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[] { Permissions.AuditView, Permissions.PlatformConfigurationView }.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
                permissionClaims);
        }
    }

    private static class TestHelpers
    {
        public static async Task<ClubStackUser> FindUserAsync(IServiceProvider services, Guid userId)
        {
            var userManager = services.GetRequiredService<UserManager<ClubStackUser>>();
            return await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("Test user not found.");
        }
    }
}