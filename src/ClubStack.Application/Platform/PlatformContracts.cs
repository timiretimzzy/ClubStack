namespace ClubStack.Application.Platform;

public sealed record ClubDto(
    Guid Id,
    string Name,
    string Code,
    string AddressLine1,
    string AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string Phone,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ClubInput(
    string Name,
    string Code,
    string AddressLine1,
    string AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string Phone,
    string Email,
    bool IsActive);

public sealed record DepartmentDto(
    Guid Id,
    Guid ClubId,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record DepartmentInput(string Name, string Code, bool IsActive);

public sealed record LocationDto(
    Guid Id,
    Guid ClubId,
    string Name,
    string Code,
    string AddressLine1,
    string AddressLine2,
    string City,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LocationInput(string Name, string Code, string AddressLine1, string AddressLine2, string City, bool IsActive);

public sealed record OutletDto(
    Guid Id,
    Guid ClubId,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OutletInput(string Name, string Code, bool IsActive);

public sealed record FacilityDto(
    Guid Id,
    Guid ClubId,
    string Name,
    string Code,
    string Description,
    int? Capacity,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record FacilityInput(string Name, string Code, string Description, int? Capacity, bool IsActive);

public sealed record PaymentMethodDto(
    Guid Id,
    string Name,
    string Code,
    string Kind,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PaymentMethodInput(string Name, string Code, string Kind, bool IsActive);

public sealed record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    int DecimalPlaces,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CurrencyInput(string Code, string Name, string Symbol, int DecimalPlaces, bool IsDefault, bool IsActive);

public sealed record TaxRateDto(
    Guid Id,
    string Name,
    decimal Rate,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TaxRateInput(string Name, decimal Rate, bool IsActive);

public sealed record NumberingSequenceDto(
    Guid Id,
    string Prefix,
    int FormatWidth,
    long LastNumber,
    string Example);

public sealed record NumberingSequenceInput(string Prefix, int FormatWidth, long LastNumber);

public sealed record SystemConfigurationDto(
    string Key,
    string Value,
    string ValueType,
    string Description,
    bool IsSystem,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAtUtc);

public sealed record SystemConfigurationInput(string Key, string Value, string ValueType, string Description);

/// <summary>
/// Reads and writes the D1 platform base configuration: club profile,
/// departments, locations, outlets, facilities, payment methods, currencies,
/// tax rates, numbering configuration and system configuration.
/// Implemented by the infrastructure persistence store.
/// </summary>
public interface IPlatformConfigurationStore
{
    /// <summary>Resolves the primary club (single-club deployment, first created) or null when not yet bootstrapped.</summary>
    Task<ClubDto?> GetPrimaryClubAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClubDto>> GetClubsAsync(CancellationToken cancellationToken = default);

    Task<ClubDto> GetClubAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ClubDto> CreateClubAsync(ClubInput input, CancellationToken cancellationToken = default);

    Task<ClubDto> UpdateClubAsync(Guid id, ClubInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default);

    Task<DepartmentDto> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DepartmentDto> CreateDepartmentAsync(Guid clubId, DepartmentInput input, CancellationToken cancellationToken = default);

    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, DepartmentInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken cancellationToken = default);

    Task<LocationDto> GetLocationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LocationDto> CreateLocationAsync(Guid clubId, LocationInput input, CancellationToken cancellationToken = default);

    Task<LocationDto> UpdateLocationAsync(Guid id, LocationInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutletDto>> GetOutletsAsync(CancellationToken cancellationToken = default);

    Task<OutletDto> GetOutletAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OutletDto> CreateOutletAsync(Guid clubId, OutletInput input, CancellationToken cancellationToken = default);

    Task<OutletDto> UpdateOutletAsync(Guid id, OutletInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacilityDto>> GetFacilitiesAsync(CancellationToken cancellationToken = default);

    Task<FacilityDto> GetFacilityAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FacilityDto> CreateFacilityAsync(Guid clubId, FacilityInput input, CancellationToken cancellationToken = default);

    Task<FacilityDto> UpdateFacilityAsync(Guid id, FacilityInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);

    Task<PaymentMethodDto> GetPaymentMethodAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaymentMethodDto> CreatePaymentMethodAsync(PaymentMethodInput input, CancellationToken cancellationToken = default);

    Task<PaymentMethodDto> UpdatePaymentMethodAsync(Guid id, PaymentMethodInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

    Task<CurrencyDto> GetCurrencyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CurrencyDto> CreateCurrencyAsync(CurrencyInput input, CancellationToken cancellationToken = default);

    Task<CurrencyDto> UpdateCurrencyAsync(Guid id, CurrencyInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxRateDto>> GetTaxRatesAsync(CancellationToken cancellationToken = default);

    Task<TaxRateDto> GetTaxRateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TaxRateDto> CreateTaxRateAsync(TaxRateInput input, CancellationToken cancellationToken = default);

    Task<TaxRateDto> UpdateTaxRateAsync(Guid id, TaxRateInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NumberingSequenceDto>> GetNumberingSequencesAsync(CancellationToken cancellationToken = default);

    Task<NumberingSequenceDto> GetNumberingSequenceAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NumberingSequenceDto> CreateNumberingSequenceAsync(NumberingSequenceInput input, CancellationToken cancellationToken = default);

    Task<NumberingSequenceDto> UpdateNumberingSequenceAsync(Guid id, NumberingSequenceInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemConfigurationDto>> GetSystemConfigurationsAsync(CancellationToken cancellationToken = default);

    Task<SystemConfigurationDto> GetSystemConfigurationAsync(string key, CancellationToken cancellationToken = default);

    Task<SystemConfigurationDto> SetSystemConfigurationAsync(SystemConfigurationInput input, string updatedByUserId, CancellationToken cancellationToken = default);
}