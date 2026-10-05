using ClubStack.Infrastructure.Bootstrap;
using ClubStack.Infrastructure.Options;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubStack.Infrastructure.Identity;

public interface IDevelopmentDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Development-only bootstrap that seeds reference data and creates a technical
/// administration user plus a demo club. Gated by <c>App:EnableDevelopmentSeed</c>
/// (default false). Credentials come from configuration (environment/user-secrets),
/// never from source control. Mirrors the controlled production bootstrap so both
/// paths converge on the same D1 acceptance state.
/// </summary>
public sealed class DevelopmentDataSeeder : IDevelopmentDataSeeder
{
    private readonly UserManager<ClubStackUser> _userManager;
    private readonly RoleManager<ClubStackRole> _roleManager;
    private readonly AppOptions _options;
    private readonly IReferenceDataSeeder _referenceDataSeeder;
    private readonly ClubStackDbContext _context;

    public DevelopmentDataSeeder(
        UserManager<ClubStackUser> userManager,
        RoleManager<ClubStackRole> roleManager,
        IOptions<AppOptions> options,
        IReferenceDataSeeder referenceDataSeeder,
        ClubStackDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _referenceDataSeeder = referenceDataSeeder;
        _context = context;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableDevelopmentSeed)
        {
            return;
        }

        var userName = _options.DevelopmentAdminUserName;
        var password = _options.DevelopmentAdminPassword;

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Development seed is enabled but 'App:DevelopmentAdminUserName' / 'App:DevelopmentAdminPassword' are not configured.");
        }

        await _referenceDataSeeder.SeedAsync(cancellationToken);

        const string roleName = BootstrapService.SystemAdministratorRole;

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{roleName}' could not be created.");
        }

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new ClubStackUser
            {
                UserName = userName,
                Email = userName,
                FirstName = "Development",
                LastName = "Administrator",
                EmailConfirmed = true,
                IsActive = true,
            };
            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Development admin user could not be created: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            await _userManager.AddToRoleAsync(user, roleName);
        }

        await EnsureDemoClubAsync(cancellationToken);
        await EnsureBootstrapMarkersAsync(cancellationToken);
    }

    private async Task EnsureDemoClubAsync(CancellationToken cancellationToken)
    {
        var club = await _context.Clubs.FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (club is not null)
        {
            return;
        }

        club = new ClubEntity
        {
            Name = "Demo Country Club",
            Code = "DEMO",
            Country = "South Africa",
            IsActive = true,
        };

        _context.Clubs.Add(club);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBootstrapMarkersAsync(CancellationToken cancellationToken)
    {
        var clubId = await _context.Clubs.AsNoTracking().Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToString("O");
        await EnsureMarkerAsync(BootstrapService.MarkerAdminCreated, "true", cancellationToken);
        await EnsureMarkerAsync(BootstrapService.MarkerClubCreated, clubId?.ToString() ?? "true", cancellationToken);
        await EnsureMarkerAsync(BootstrapService.MarkerCompleted, now, cancellationToken);
    }

    private async Task EnsureMarkerAsync(string key, string value, CancellationToken cancellationToken)
    {
        var entity = await _context.SystemConfigurations.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (entity is null)
        {
            _context.SystemConfigurations.Add(new SystemConfigurationEntity
            {
                Key = key,
                Value = value,
                ValueType = "String",
                Description = "Controlled bootstrap marker (Phase 9 §27).",
                IsSystem = true,
                UpdatedByUserId = "bootstrap",
            });
            return;
        }

        if (string.IsNullOrEmpty(entity.Value))
        {
            entity.Value = value;
        }
    }
}