using ClubStack.Application.Exceptions;
using ClubStack.Application.Platform;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Platform;

/// <inheritdoc />
public sealed class EntityFrameworkPlatformConfigurationStore : IPlatformConfigurationStore
{
    private readonly ClubStackDbContext _context;

    public EntityFrameworkPlatformConfigurationStore(ClubStackDbContext context)
    {
        _context = context;
    }

    public Task<ClubDto?> GetPrimaryClubAsync(CancellationToken cancellationToken = default)
    {
        return _context.Clubs
            .AsNoTracking()
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => ToClub(c))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClubDto>> GetClubsAsync(CancellationToken cancellationToken = default)
    {
        var clubs = await _context.Clubs.AsNoTracking().OrderBy(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
        return clubs.Select(ToClub).ToList();
    }

    public async Task<ClubDto> GetClubAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var club = await _context.Clubs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                   ?? throw new NotFoundException($"Club '{id}' was not found.");
        return ToClub(club);
    }

    public async Task<ClubDto> CreateClubAsync(ClubInput input, CancellationToken cancellationToken = default)
    {
        if (await _context.Clubs.AnyAsync(c => c.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"A club with code '{input.Code}' already exists.");
        }

        var entity = new ClubEntity
        {
            Name = input.Name,
            Code = input.Code,
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            City = input.City,
            Region = input.Region,
            PostalCode = input.PostalCode,
            Country = input.Country,
            Phone = input.Phone,
            Email = input.Email,
            IsActive = input.IsActive,
        };

        _context.Clubs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToClub(entity);
    }

    public async Task<ClubDto> UpdateClubAsync(Guid id, ClubInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Clubs.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Club '{id}' was not found.");

        if (await _context.Clubs.AnyAsync(c => c.Code == input.Code && c.Id != id, cancellationToken))
        {
            throw new ConflictException($"A club with code '{input.Code}' already exists.");
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.AddressLine1 = input.AddressLine1;
        entity.AddressLine2 = input.AddressLine2;
        entity.City = input.City;
        entity.Region = input.Region;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return ToClub(entity);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        return rows.Select(ToDepartment).ToList();
    }

    public async Task<DepartmentDto> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Department '{id}' was not found.");
        return ToDepartment(row);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(Guid clubId, DepartmentInput input, CancellationToken cancellationToken = default)
    {
        await EnsureClubAsync(clubId, cancellationToken);
        if (await _context.Departments.AnyAsync(d => d.ClubId == clubId && d.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"A department with code '{input.Code}' already exists for this club.");
        }

        var entity = new DepartmentEntity { ClubId = clubId, Name = input.Name, Code = input.Code, IsActive = input.IsActive };
        _context.Departments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDepartment(entity);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, DepartmentInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Department '{id}' was not found.");

        if (await _context.Departments.AnyAsync(d => d.ClubId == entity.ClubId && d.Code == input.Code && d.Id != id, cancellationToken))
        {
            throw new ConflictException($"A department with code '{input.Code}' already exists for this club.");
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToDepartment(entity);
    }

    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(cancellationToken);
        return rows.Select(ToLocation).ToList();
    }

    public async Task<LocationDto> GetLocationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Location '{id}' was not found.");
        return ToLocation(row);
    }

    public async Task<LocationDto> CreateLocationAsync(Guid clubId, LocationInput input, CancellationToken cancellationToken = default)
    {
        await EnsureClubAsync(clubId, cancellationToken);
        if (await _context.Locations.AnyAsync(l => l.ClubId == clubId && l.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"A location with code '{input.Code}' already exists for this club.");
        }

        var entity = new LocationEntity
        {
            ClubId = clubId,
            Name = input.Name,
            Code = input.Code,
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            City = input.City,
            IsActive = input.IsActive,
        };
        _context.Locations.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToLocation(entity);
    }

    public async Task<LocationDto> UpdateLocationAsync(Guid id, LocationInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Location '{id}' was not found.");

        if (await _context.Locations.AnyAsync(l => l.ClubId == entity.ClubId && l.Code == input.Code && l.Id != id, cancellationToken))
        {
            throw new ConflictException($"A location with code '{input.Code}' already exists for this club.");
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.AddressLine1 = input.AddressLine1;
        entity.AddressLine2 = input.AddressLine2;
        entity.City = input.City;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToLocation(entity);
    }

    public async Task<IReadOnlyList<OutletDto>> GetOutletsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Outlets.AsNoTracking().OrderBy(o => o.Name).ToListAsync(cancellationToken);
        return rows.Select(ToOutlet).ToList();
    }

    public async Task<OutletDto> GetOutletAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.Outlets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Outlet '{id}' was not found.");
        return ToOutlet(row);
    }

    public async Task<OutletDto> CreateOutletAsync(Guid clubId, OutletInput input, CancellationToken cancellationToken = default)
    {
        await EnsureClubAsync(clubId, cancellationToken);
        if (await _context.Outlets.AnyAsync(o => o.ClubId == clubId && o.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"An outlet with code '{input.Code}' already exists for this club.");
        }

        var entity = new OutletEntity { ClubId = clubId, Name = input.Name, Code = input.Code, IsActive = input.IsActive };
        _context.Outlets.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToOutlet(entity);
    }

    public async Task<OutletDto> UpdateOutletAsync(Guid id, OutletInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Outlets.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Outlet '{id}' was not found.");

        if (await _context.Outlets.AnyAsync(o => o.ClubId == entity.ClubId && o.Code == input.Code && o.Id != id, cancellationToken))
        {
            throw new ConflictException($"An outlet with code '{input.Code}' already exists for this club.");
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToOutlet(entity);
    }

    public async Task<IReadOnlyList<FacilityDto>> GetFacilitiesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Facilities.AsNoTracking().OrderBy(f => f.Name).ToListAsync(cancellationToken);
        return rows.Select(ToFacility).ToList();
    }

    public async Task<FacilityDto> GetFacilityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Facility '{id}' was not found.");
        return ToFacility(row);
    }

    public async Task<FacilityDto> CreateFacilityAsync(Guid clubId, FacilityInput input, CancellationToken cancellationToken = default)
    {
        await EnsureClubAsync(clubId, cancellationToken);
        if (await _context.Facilities.AnyAsync(f => f.ClubId == clubId && f.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"A facility with code '{input.Code}' already exists for this club.");
        }

        var entity = new FacilityEntity
        {
            ClubId = clubId,
            Name = input.Name,
            Code = input.Code,
            Description = input.Description,
            Capacity = input.Capacity,
            IsActive = input.IsActive,
        };
        _context.Facilities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToFacility(entity);
    }

    public async Task<FacilityDto> UpdateFacilityAsync(Guid id, FacilityInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Facility '{id}' was not found.");

        if (await _context.Facilities.AnyAsync(f => f.ClubId == entity.ClubId && f.Code == input.Code && f.Id != id, cancellationToken))
        {
            throw new ConflictException($"A facility with code '{input.Code}' already exists for this club.");
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.Description = input.Description;
        entity.Capacity = input.Capacity;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToFacility(entity);
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.PaymentMethods.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return rows.Select(ToPaymentMethod).ToList();
    }

    public async Task<PaymentMethodDto> GetPaymentMethodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Payment method '{id}' was not found.");
        return ToPaymentMethod(row);
    }

    public async Task<PaymentMethodDto> CreatePaymentMethodAsync(PaymentMethodInput input, CancellationToken cancellationToken = default)
    {
        if (await _context.PaymentMethods.AnyAsync(p => p.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"A payment method with code '{input.Code}' already exists.");
        }

        var entity = new PaymentMethodEntity { Name = input.Name, Code = input.Code, Kind = NormalizeKind(input.Kind), IsActive = input.IsActive };
        _context.PaymentMethods.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToPaymentMethod(entity);
    }

    public async Task<PaymentMethodDto> UpdatePaymentMethodAsync(Guid id, PaymentMethodInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Payment method '{id}' was not found.");

        if (await _context.PaymentMethods.AnyAsync(p => p.Code == input.Code && p.Id != id, cancellationToken))
        {
            throw new ConflictException($"A payment method with code '{input.Code}' already exists.");
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.Kind = NormalizeKind(input.Kind);
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToPaymentMethod(entity);
    }

    public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Currencies.AsNoTracking().OrderBy(c => c.Code).ToListAsync(cancellationToken);
        return rows.Select(ToCurrency).ToList();
    }

    public async Task<CurrencyDto> GetCurrencyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Currency '{id}' was not found.");
        return ToCurrency(row);
    }

    public async Task<CurrencyDto> CreateCurrencyAsync(CurrencyInput input, CancellationToken cancellationToken = default)
    {
        if (await _context.Currencies.AnyAsync(c => c.Code == input.Code, cancellationToken))
        {
            throw new ConflictException($"A currency with code '{input.Code}' already exists.");
        }

        if (input.IsDefault && await _context.Currencies.AnyAsync(c => c.IsDefault, cancellationToken))
        {
            throw new ConflictException("A default currency is already configured.");
        }

        var entity = new CurrencyEntity
        {
            Code = input.Code,
            Name = input.Name,
            Symbol = input.Symbol,
            DecimalPlaces = input.DecimalPlaces,
            IsDefault = input.IsDefault,
            IsActive = input.IsActive,
        };
        _context.Currencies.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToCurrency(entity);
    }

    public async Task<CurrencyDto> UpdateCurrencyAsync(Guid id, CurrencyInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Currency '{id}' was not found.");

        if (await _context.Currencies.AnyAsync(c => c.Code == input.Code && c.Id != id, cancellationToken))
        {
            throw new ConflictException($"A currency with code '{input.Code}' already exists.");
        }

        if (input.IsDefault && await _context.Currencies.AnyAsync(c => c.IsDefault && c.Id != id, cancellationToken))
        {
            throw new ConflictException("A default currency is already configured.");
        }

        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.Symbol = input.Symbol;
        entity.DecimalPlaces = input.DecimalPlaces;
        entity.IsDefault = input.IsDefault;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToCurrency(entity);
    }

    public async Task<IReadOnlyList<TaxRateDto>> GetTaxRatesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.TaxRates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return rows.Select(ToTaxRate).ToList();
    }

    public async Task<TaxRateDto> GetTaxRateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.TaxRates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Tax rate '{id}' was not found.");
        return ToTaxRate(row);
    }

    public async Task<TaxRateDto> CreateTaxRateAsync(TaxRateInput input, CancellationToken cancellationToken = default)
    {
        if (await _context.TaxRates.AnyAsync(t => t.Name == input.Name, cancellationToken))
        {
            throw new ConflictException($"A tax rate named '{input.Name}' already exists.");
        }

        var entity = new TaxRateEntity { Name = input.Name, Rate = input.Rate, IsActive = input.IsActive };
        _context.TaxRates.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToTaxRate(entity);
    }

    public async Task<TaxRateDto> UpdateTaxRateAsync(Guid id, TaxRateInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TaxRates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Tax rate '{id}' was not found.");

        if (await _context.TaxRates.AnyAsync(t => t.Name == input.Name && t.Id != id, cancellationToken))
        {
            throw new ConflictException($"A tax rate named '{input.Name}' already exists.");
        }

        entity.Name = input.Name;
        entity.Rate = input.Rate;
        entity.IsActive = input.IsActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToTaxRate(entity);
    }

    public async Task<IReadOnlyList<NumberingSequenceDto>> GetNumberingSequencesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.ReferenceSequences.AsNoTracking().OrderBy(r => r.Prefix).ToListAsync(cancellationToken);
        return rows.Select(ToNumberingSequence).ToList();
    }

    public async Task<NumberingSequenceDto> GetNumberingSequenceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.ReferenceSequences.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new NotFoundException($"Numbering sequence '{id}' was not found.");
        return ToNumberingSequence(row);
    }

    public async Task<NumberingSequenceDto> CreateNumberingSequenceAsync(NumberingSequenceInput input, CancellationToken cancellationToken = default)
    {
        if (await _context.ReferenceSequences.AnyAsync(r => r.Prefix == input.Prefix, cancellationToken))
        {
            throw new ConflictException($"A numbering sequence with prefix '{input.Prefix}' already exists.");
        }

        var entity = new ReferenceSequenceEntity
        {
            Prefix = input.Prefix,
            FormatWidth = input.FormatWidth,
            LastNumber = input.LastNumber,
        };
        _context.ReferenceSequences.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToNumberingSequence(entity);
    }

    public async Task<NumberingSequenceDto> UpdateNumberingSequenceAsync(Guid id, NumberingSequenceInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ReferenceSequences.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Numbering sequence '{id}' was not found.");

        if (await _context.ReferenceSequences.AnyAsync(r => r.Prefix == input.Prefix && r.Id != id, cancellationToken))
        {
            throw new ConflictException($"A numbering sequence with prefix '{input.Prefix}' already exists.");
        }

        entity.Prefix = input.Prefix;
        entity.FormatWidth = input.FormatWidth;
        entity.LastNumber = input.LastNumber;
        await _context.SaveChangesAsync(cancellationToken);
        return ToNumberingSequence(entity);
    }

    public async Task<IReadOnlyList<SystemConfigurationDto>> GetSystemConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.SystemConfigurations.AsNoTracking().OrderBy(s => s.Key).ToListAsync(cancellationToken);
        return rows.Select(ToSystemConfiguration).ToList();
    }

    public async Task<SystemConfigurationDto> GetSystemConfigurationAsync(string key, CancellationToken cancellationToken = default)
    {
        var row = await _context.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, cancellationToken)
                  ?? throw new NotFoundException($"System configuration '{key}' was not found.");
        return ToSystemConfiguration(row);
    }

    public async Task<SystemConfigurationDto> SetSystemConfigurationAsync(SystemConfigurationInput input, string updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SystemConfigurations.FirstOrDefaultAsync(s => s.Key == input.Key, cancellationToken);

        if (entity is null)
        {
            entity = new SystemConfigurationEntity { Key = input.Key };
            _context.SystemConfigurations.Add(entity);
        }

        if (entity.IsSystem)
        {
            throw new ForbiddenException($"System configuration '{input.Key}' is protected and cannot be modified.");
        }

        entity.Value = input.Value;
        entity.ValueType = input.ValueType;
        entity.Description = input.Description;
        entity.UpdatedByUserId = updatedByUserId;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return ToSystemConfiguration(entity);
    }

    private async Task EnsureClubAsync(Guid clubId, CancellationToken cancellationToken)
    {
        if (!await _context.Clubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            throw new NotFoundException($"Club '{clubId}' was not found.");
        }
    }

    private static string NormalizeKind(string kind)
    {
        var normalized = kind?.Trim() ?? string.Empty;
        return normalized switch
        {
            "Cash" or "cash" => "Cash",
            "Card" or "card" => "Card",
            "Account" or "account" => "Account",
            _ => "Other",
        };
    }

    private static ClubDto ToClub(ClubEntity e) => new(
        e.Id, e.Name, e.Code, e.AddressLine1, e.AddressLine2, e.City, e.Region, e.PostalCode, e.Country, e.Phone, e.Email,
        e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static DepartmentDto ToDepartment(DepartmentEntity e) => new(
        e.Id, e.ClubId, e.Name, e.Code, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static LocationDto ToLocation(LocationEntity e) => new(
        e.Id, e.ClubId, e.Name, e.Code, e.AddressLine1, e.AddressLine2, e.City, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static OutletDto ToOutlet(OutletEntity e) => new(
        e.Id, e.ClubId, e.Name, e.Code, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static FacilityDto ToFacility(FacilityEntity e) => new(
        e.Id, e.ClubId, e.Name, e.Code, e.Description, e.Capacity, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static PaymentMethodDto ToPaymentMethod(PaymentMethodEntity e) => new(
        e.Id, e.Name, e.Code, e.Kind, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static CurrencyDto ToCurrency(CurrencyEntity e) => new(
        e.Id, e.Code, e.Name, e.Symbol, e.DecimalPlaces, e.IsDefault, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static TaxRateDto ToTaxRate(TaxRateEntity e) => new(
        e.Id, e.Name, e.Rate, e.IsActive, e.CreatedAtUtc, e.UpdatedAtUtc);

    private static NumberingSequenceDto ToNumberingSequence(ReferenceSequenceEntity e) => new(
        e.Id, e.Prefix, e.FormatWidth, e.LastNumber, $"{e.Prefix}{e.LastNumber.ToString($"D{e.FormatWidth}")}");

    private static SystemConfigurationDto ToSystemConfiguration(SystemConfigurationEntity e) => new(
        e.Key, e.Value, e.ValueType, e.Description, e.IsSystem, e.UpdatedByUserId, e.UpdatedAtUtc);
}