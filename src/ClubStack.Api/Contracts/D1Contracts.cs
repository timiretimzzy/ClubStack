using System.ComponentModel.DataAnnotations;

namespace ClubStack.Api.Contracts;

// ---- Bootstrap (Phase 9 §27) ----

public sealed record BootstrapStatusResponse
{
    public bool Completed { get; init; }

    public bool AdministratorCreated { get; init; }

    public bool ClubCreated { get; init; }

    public Guid? ClubId { get; init; }

    public string? CompletedAtUtc { get; init; }
}

public sealed record CreateAdministratorRequest
{
    [Required, MaxLength(256)]
    public string UserName { get; init; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(12)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(128)]
    public string FirstName { get; init; } = string.Empty;

    [MaxLength(128)]
    public string LastName { get; init; } = string.Empty;
}

public sealed record CreateClubRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(8)]
    public string? CurrencyCode { get; init; }

    [MaxLength(64)]
    public string? Country { get; init; }
}

// ---- Clubs ----

public sealed record ClubRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(256)]
    public string AddressLine1 { get; init; } = string.Empty;

    [MaxLength(256)]
    public string AddressLine2 { get; init; } = string.Empty;

    [MaxLength(128)]
    public string City { get; init; } = string.Empty;

    [MaxLength(128)]
    public string Region { get; init; } = string.Empty;

    [MaxLength(32)]
    public string PostalCode { get; init; } = string.Empty;

    [MaxLength(64)]
    public string Country { get; init; } = string.Empty;

    [MaxLength(32)]
    public string Phone { get; init; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

public sealed record ClubResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string AddressLine2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Departments ----

public sealed record DepartmentRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

public sealed record DepartmentResponse
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Locations ----

public sealed record LocationRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(256)]
    public string AddressLine1 { get; init; } = string.Empty;

    [MaxLength(256)]
    public string AddressLine2 { get; init; } = string.Empty;

    [MaxLength(128)]
    public string City { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

public sealed record LocationResponse
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string AddressLine2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Outlets ----

public sealed record OutletRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

public sealed record OutletResponse
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Facilities ----

public sealed record FacilityRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(512)]
    public string Description { get; init; } = string.Empty;

    public int? Capacity { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record FacilityResponse
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? Capacity { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Payment methods ----

public sealed record PaymentMethodRequest
{
    [Required, MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(24)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(24)]
    public string Kind { get; init; } = "Other";

    public bool IsActive { get; init; } = true;
}

public sealed record PaymentMethodResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Currencies ----

public sealed record CurrencyRequest
{
    [Required, MaxLength(8)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(8)]
    public string Symbol { get; init; } = string.Empty;

    [Range(0, 8)]
    public int DecimalPlaces { get; init; } = 2;

    public bool IsDefault { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record CurrencyResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public int DecimalPlaces { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Tax rates ----

public sealed record TaxRateRequest
{
    [Required, MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 1)]
    public decimal Rate { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record TaxRateResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Numbering sequences ----

public sealed record NumberingSequenceRequest
{
    [Required, MaxLength(16)]
    public string Prefix { get; init; } = string.Empty;

    [Range(1, 12)]
    public int FormatWidth { get; init; } = 6;

    public long LastNumber { get; init; }
}

public sealed record NumberingSequenceResponse
{
    public Guid Id { get; init; }
    public string Prefix { get; init; } = string.Empty;
    public int FormatWidth { get; init; }
    public long LastNumber { get; init; }
    public string Example { get; init; } = string.Empty;
}

// ---- System configuration ----

public sealed record SystemConfigurationRequest
{
    [Required, MaxLength(128)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Value { get; init; } = string.Empty;

    [Required, MaxLength(24)]
    public string ValueType { get; init; } = "String";

    [MaxLength(512)]
    public string Description { get; init; } = string.Empty;
}

public sealed record SystemConfigurationResponse
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public string UpdatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Users ----

public sealed record UserResponse
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginAtUtc { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public sealed record CreateUserRequest
{
    [Required, MaxLength(256)]
    public string UserName { get; init; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(128)]
    public string FirstName { get; init; } = string.Empty;

    [MaxLength(128)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(32)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, MinLength(12)]
    public string Password { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public sealed record UpdateUserRequest
{
    [MaxLength(128)]
    public string FirstName { get; init; } = string.Empty;

    [MaxLength(128)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(32)]
    public string PhoneNumber { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public sealed record ResetPasswordRequest
{
    [Required, MinLength(12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record DeactivateUserRequest
{
    [MaxLength(256)]
    public string Reason { get; init; } = string.Empty;
}

// ---- Roles ----

public sealed record RoleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

public sealed record CreateRoleRequest
{
    [Required, MaxLength(256)]
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

public sealed record UpdateRoleRequest
{
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

// ---- Permissions ----

public sealed record PermissionResponse
{
    public string Key { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsSensitive { get; init; }
}

// ---- Sessions ----

public sealed record SessionResponse
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset LastSeenAtUtc { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
    public string RevocationReason { get; init; } = string.Empty;
}

public sealed record RevokeSessionRequest
{
    [MaxLength(256)]
    public string Reason { get; init; } = string.Empty;
}

// ---- Audit ----

public sealed record AuditEntryResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string MetadataJson { get; init; } = string.Empty;
}

public sealed record AuditQueryResponse
{
    public IReadOnlyList<AuditEntryResponse> Items { get; init; } = Array.Empty<AuditEntryResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}