using ClubStack.Application.Bootstrap;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Identity;
using ClubStack.Application.Platform;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Options;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubStack.Infrastructure.Bootstrap;

/// <inheritdoc />
public sealed class BootstrapService : IBootstrapService
{
    public const string MarkerAdminCreated = "core.bootstrap.admin_created";
    public const string MarkerClubCreated = "core.bootstrap.club_created";
    public const string MarkerCompleted = "core.bootstrap.completed";
    public const string SystemAdministratorRole = "System Administrator";

    private readonly IReferenceDataSeeder _referenceDataSeeder;
    private readonly Finance.FinanceReferenceSeeder _financeReferenceSeeder;
    private readonly IIdentityAdministrationService _identityAdministrationService;
    private readonly IPlatformConfigurationStore _platformConfigurationStore;
    private readonly ClubStackDbContext _context;
    private readonly AppOptions _appOptions;

    public BootstrapService(
        IReferenceDataSeeder referenceDataSeeder,
        Finance.FinanceReferenceSeeder financeReferenceSeeder,
        IIdentityAdministrationService identityAdministrationService,
        IPlatformConfigurationStore platformConfigurationStore,
        ClubStackDbContext context,
        IOptions<AppOptions> appOptions)
    {
        _referenceDataSeeder = referenceDataSeeder;
        _financeReferenceSeeder = financeReferenceSeeder;
        _identityAdministrationService = identityAdministrationService;
        _platformConfigurationStore = platformConfigurationStore;
        _context = context;
        _appOptions = appOptions.Value;
    }

    public async Task<BootstrapStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var markers = await _context.SystemConfigurations
            .AsNoTracking()
            .Where(s => s.Key == MarkerAdminCreated || s.Key == MarkerClubCreated || s.Key == MarkerCompleted)
            .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal, cancellationToken);

        var adminCreated = markers.TryGetValue(MarkerAdminCreated, out var adminValue) && adminValue == "true";
        var clubValue = markers.GetValueOrDefault(MarkerClubCreated);
        var clubCreated = !string.IsNullOrEmpty(clubValue);
        var completedValue = markers.GetValueOrDefault(MarkerCompleted);
        var completed = adminCreated && clubCreated && !string.IsNullOrEmpty(completedValue);

        Guid? clubId = clubCreated && Guid.TryParse(clubValue, out var parsed) ? parsed : null;

        return new BootstrapStatus(completed, adminCreated, clubCreated, clubId, clubCreated ? completedValue : null);
    }

    public async Task<BootstrapStatus> CreateAdministratorAsync(CreateAdministratorInput input, CancellationToken cancellationToken = default)
    {
        EnsureBootstrapEnabled();

        var status = await GetStatusAsync(cancellationToken);
        if (status.AdministratorCreated)
        {
            throw new ConflictException("Bootstrap administrator creation has already been completed.");
        }

        await _referenceDataSeeder.SeedAsync(cancellationToken);
        await _financeReferenceSeeder.SeedAsync(cancellationToken);

        await _identityAdministrationService.CreateUserAsync(new CreateUserInput(
            input.UserName,
            input.Email,
            input.FirstName,
            input.LastName,
            string.Empty,
            input.Password,
            [SystemAdministratorRole]), cancellationToken);

        await SetMarkerAsync(MarkerAdminCreated, "true", cancellationToken);

        var refreshed = await GetStatusAsync(cancellationToken);
        if (refreshed.ClubCreated)
        {
            await SetCompletedMarkerAsync(cancellationToken);
            return await GetStatusAsync(cancellationToken);
        }

        return refreshed;
    }

    public async Task<BootstrapStatus> CreateClubAsync(CreateClubInput input, CancellationToken cancellationToken = default)
    {
        EnsureBootstrapEnabled();

        var status = await GetStatusAsync(cancellationToken);
        if (status.ClubCreated)
        {
            throw new ConflictException("Bootstrap club creation has already been completed.");
        }

        await _referenceDataSeeder.SeedAsync(cancellationToken);
        await _financeReferenceSeeder.SeedAsync(cancellationToken);

        var club = await _platformConfigurationStore.CreateClubAsync(new ClubInput(
            input.Name,
            input.Code,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            input.Country ?? "South Africa",
            string.Empty,
            string.Empty,
            IsActive: true), cancellationToken);

        var currencyCode = input.CurrencyCode ?? "ZAR";
        var currencies = await _platformConfigurationStore.GetCurrenciesAsync(cancellationToken);
        if (currencies.All(c => !string.Equals(c.Code, currencyCode, StringComparison.OrdinalIgnoreCase)))
        {
            await _platformConfigurationStore.CreateCurrencyAsync(new CurrencyInput(
                currencyCode,
                currencyCode,
                currencyCode,
                2,
                currencies.Count == 0,
                IsActive: true), cancellationToken);
        }

        await SetMarkerAsync(MarkerClubCreated, club.Id.ToString(), cancellationToken);

        var refreshed = await GetStatusAsync(cancellationToken);
        if (refreshed.AdministratorCreated)
        {
            await SetCompletedMarkerAsync(cancellationToken);
            return await GetStatusAsync(cancellationToken);
        }

        return refreshed;
    }

    private void EnsureBootstrapEnabled()
    {
        if (!_appOptions.EnableBootstrap && !_appOptions.EnableDevelopmentSeed)
        {
            throw new ForbiddenException(
                "Controlled bootstrap is disabled. Set 'App:EnableBootstrap' to true when the first-run bootstrap is supplied.");
        }
    }

    private async Task SetMarkerAsync(string key, string value, CancellationToken cancellationToken)
    {
        var entity = await _context.SystemConfigurations.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (entity is null)
        {
            entity = new SystemConfigurationEntity
            {
                Key = key,
                Value = value,
                ValueType = "String",
                Description = "Controlled bootstrap marker (Phase 9 §27).",
                IsSystem = true,
                UpdatedByUserId = "bootstrap",
            };
            _context.SystemConfigurations.Add(entity);
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SetCompletedMarkerAsync(CancellationToken cancellationToken)
    {
        var entity = await _context.SystemConfigurations.FirstOrDefaultAsync(s => s.Key == MarkerCompleted, cancellationToken);
        if (entity is null)
        {
            entity = new SystemConfigurationEntity
            {
                Key = MarkerCompleted,
                Value = DateTimeOffset.UtcNow.ToString("O"),
                ValueType = "String",
                Description = "Signals that first-run bootstrap is complete and can no longer run.",
                IsSystem = true,
                UpdatedByUserId = "bootstrap",
            };
            _context.SystemConfigurations.Add(entity);
        }
        else
        {
            entity.Value = DateTimeOffset.UtcNow.ToString("O");
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}