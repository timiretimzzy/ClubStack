namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Club profile record (Phase 8 §8.1 <c>clubs</c>). Single-club deployment;
/// the club column model keeps multi-club readiness explicit (Phase 9 §43).
/// </summary>
public sealed class ClubEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string AddressLine2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Club department (Phase 8 §8.1 <c>departments</c>). Club-scoped.</summary>
public sealed class DepartmentEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ClubId { get; set; }

    public ClubEntity Club { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Operational location (Phase 8 §8.1 <c>locations</c>). Club-scoped.</summary>
public sealed class LocationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ClubId { get; set; }

    public ClubEntity Club { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string AddressLine2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Operational outlet (Phase 8 §8.1 <c>outlets</c>). Club-scoped.</summary>
public sealed class OutletEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ClubId { get; set; }

    public ClubEntity Club { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Facility base configuration (Phase 8 §8.1 <c>facilities</c>, Phase 6 D1
/// "facilities base configuration"). The reservation engine is a later slice
/// (D13); only the registrar is established here. Club-scoped.
/// </summary>
public sealed class FacilityEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ClubId { get; set; }

    public ClubEntity Club { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Payment method reference data (Phase 8 §8.1 <c>payment_methods</c>).</summary>
public sealed class PaymentMethodEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>Cash, Card, Account or Other (split-payment categories, Phase 5 §40).</summary>
    public string Kind { get; set; } = "Other";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Currency reference data (Phase 8 §8.1 <c>currencies</c>).</summary>
public sealed class CurrencyEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public int DecimalPlaces { get; set; } = 2;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Tax rate reference data (Phase 8 §8.1 <c>tax_rates</c>).</summary>
public sealed class TaxRateEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    /// <summary>Applicable fractional rate (e.g. 0.1500 for 15%).</summary>
    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// System configuration key/value store (Phase 8 §8.1 <c>system_configurations</c>).
/// Values are stored as text with a declared value type; typed parsing is the
/// caller's responsibility.
/// </summary>
public sealed class SystemConfigurationEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string ValueType { get; set; } = "String";

    public string Description { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public string UpdatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}