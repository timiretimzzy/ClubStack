using System.ComponentModel.DataAnnotations;

namespace ClubStack.Api.Contracts;

// ---- Persons ----

public sealed record PersonRequest
{
    [Required, MaxLength(160)]
    public string LegalName { get; init; } = string.Empty;

    [MaxLength(160)]
    public string PreferredName { get; init; } = string.Empty;

    [MaxLength(64)]
    public string? IdNumber { get; init; }

    public DateTimeOffset? DateOfBirth { get; init; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

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

    [MaxLength(1024)]
    public string Notes { get; init; } = string.Empty;
}

public sealed record PersonResponse
{
    public Guid Id { get; init; }
    public string LegalName { get; init; } = string.Empty;
    public string PreferredName { get; init; } = string.Empty;
    public Guid? MemberId { get; init; }
    public string? MemberNumber { get; init; }
    public string? IdNumber { get; init; }
    public DateTimeOffset? DateOfBirth { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string AddressLine1 { get; init; } = string.Empty;
    public string AddressLine2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>Person creation outcome that may carry likely-duplicate warnings (Phase 2 MEM-003).</summary>
public sealed record PersonCreatedResponse
{
    public PersonResponse Person { get; init; } = new();
    public IReadOnlyList<string> DuplicateWarnings { get; init; } = Array.Empty<string>();
}

public sealed record PersonSearchResponse
{
    public IReadOnlyList<PersonResponse> Items { get; init; } = Array.Empty<PersonResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

// ---- Members ----

public sealed record MemberResponse
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public string LegalName { get; init; } = string.Empty;
    public string PreferredName { get; init; } = string.Empty;
    public string MemberNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed record MemberSearchResponse
{
    public IReadOnlyList<MemberResponse> Items { get; init; } = Array.Empty<MemberResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record MemberDetailResponse
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public string LegalName { get; init; } = string.Empty;
    public string PreferredName { get; init; } = string.Empty;
    public string MemberNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public PersonResponse Person { get; init; } = new();
    public IReadOnlyList<MembershipResponse> Memberships { get; init; } = Array.Empty<MembershipResponse>();
    public IReadOnlyList<MembershipDependantResponse> Dependants { get; init; } = Array.Empty<MembershipDependantResponse>();
}

// ---- Membership categories ----

public sealed record MembershipCategoryRequest
{
    [Required, MaxLength(16)]
    public string Code { get; init; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string Description { get; init; } = string.Empty;

    [MaxLength(1024)]
    public string EligibilityRules { get; init; } = string.Empty;

    [Range(1, 120)]
    public int RenewalPeriodMonths { get; init; } = 12;

    [MaxLength(4096)]
    public string PrivilegeConfiguration { get; init; } = string.Empty;

    [MaxLength(1024)]
    public string GuestRules { get; init; } = string.Empty;

    [MaxLength(1024)]
    public string AccountRules { get; init; } = string.Empty;

    [MaxLength(1024)]
    public string CredentialRules { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

public sealed record MembershipCategoryResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string EligibilityRules { get; init; } = string.Empty;
    public int RenewalPeriodMonths { get; init; }
    public string PrivilegeConfiguration { get; init; } = string.Empty;
    public string GuestRules { get; init; } = string.Empty;
    public string AccountRules { get; init; } = string.Empty;
    public string CredentialRules { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record MembershipFeeRequest
{
    [Required, MaxLength(24)]
    public string FeeType { get; init; } = string.Empty;

    [MaxLength(256)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 99_999_999.99)]
    public decimal Amount { get; init; }

    [Required, MaxLength(8)]
    public string CurrencyCode { get; init; } = "ZAR";

    public bool IsProrationEnabled { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record MembershipFeeResponse
{
    public Guid Id { get; init; }
    public Guid CategoryId { get; init; }
    public string FeeType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsProrationEnabled { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

// ---- Membership applications ----

public sealed record CreateMembershipApplicationRequest
{
    public required Guid PersonId { get; init; }

    public required Guid CategoryId { get; init; }

    [MaxLength(160)]
    public string? SponsorName { get; init; }

    [MaxLength(160)]
    public string? ReferrerName { get; init; }

    [MaxLength(2048)]
    public string Notes { get; init; } = string.Empty;

    [MaxLength(2048)]
    public string RequiredDocuments { get; init; } = string.Empty;
}

public sealed record RejectMembershipApplicationRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record MembershipApplicationResponse
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? SponsorName { get; init; }
    public string? ReferrerName { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string? ApprovedByUserId { get; init; }
    public string RequiredDocuments { get; init; } = string.Empty;
    public DateTimeOffset? SubmittedAtUtc { get; init; }
    public DateTimeOffset? ReviewedAtUtc { get; init; }
    public DateTimeOffset? ApprovedAtUtc { get; init; }
    public string? RejectionReason { get; init; }
    public Guid? MemberId { get; init; }
    public string? MemberNumber { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record MembershipApplicationSearchResponse
{
    public IReadOnlyList<MembershipApplicationResponse> Items { get; init; } = Array.Empty<MembershipApplicationResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

// ---- Memberships ----

public sealed record MembershipResponse
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? StartDateUtc { get; init; }
    public DateTimeOffset? EndDateUtc { get; init; }
    public DateTimeOffset? RenewalDateUtc { get; init; }
    public string? ApprovedByUserId { get; init; }
    public DateTimeOffset? ApprovedAtUtc { get; init; }
    public DateTimeOffset? ActivatedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record MembershipStatusHistoryResponse
{
    public Guid Id { get; init; }
    public Guid MembershipId { get; init; }
    public string PreviousState { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public string ChangedByUserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; init; }
}

public sealed record MembershipTransitionRequest
{
    [MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record TransferMembershipRequest
{
    public required Guid TargetCategoryId { get; init; }
}

// ---- Households ----

public sealed record HouseholdRequest
{
    [Required, MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    public Guid? PrimaryMemberId { get; init; }
}

public sealed record AddHouseholdMemberRequest
{
    public required Guid PersonId { get; init; }

    [Required, MaxLength(24)]
    public string Role { get; init; } = string.Empty;
}

public sealed record HouseholdMemberResponse
{
    public Guid HouseholdId { get; init; }
    public Guid PersonId { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public string? MemberNumber { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed record HouseholdResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public Guid? PrimaryMemberId { get; init; }
    public string? PrimaryMemberName { get; init; }
    public string? PrimaryMemberNumber { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<HouseholdMemberResponse> Members { get; init; } = Array.Empty<HouseholdMemberResponse>();
}

// ---- Membership dependants ----

public sealed record MembershipDependantResponse
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public Guid DependantPersonId { get; init; }
    public string DependantName { get; init; } = string.Empty;
    public string RelationType { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed record AddDependantRequest
{
    public required Guid DependantPersonId { get; init; }

    [Required, MaxLength(24)]
    public string RelationType { get; init; } = string.Empty;

    [MaxLength(512)]
    public string Notes { get; init; } = string.Empty;
}

// ---- Fee assessments ----

public sealed record AssessFeeRequest
{
    public required Guid MembershipId { get; init; }

    [Required, MaxLength(24)]
    public string FeeType { get; init; } = string.Empty;

    public DateTimeOffset? AssessmentDateUtc { get; init; }
}

public sealed record FeeAdjustmentRequest
{
    [Range(0.01, 99_999_999.99)]
    public decimal Amount { get; init; }

    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record FeeVoidRequest
{
    [Required, MaxLength(512)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record MembershipFeeAssessmentResponse
{
    public Guid Id { get; init; }
    public Guid MembershipId { get; init; }
    public string MembershipNumber { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public string FeeType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsProrated { get; init; }
    public DateTimeOffset AssessmentDateUtc { get; init; }
    public DateTimeOffset? DueDateUtc { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record MembershipFeeAssessmentSearchResponse
{
    public IReadOnlyList<MembershipFeeAssessmentResponse> Items { get; init; } = Array.Empty<MembershipFeeAssessmentResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}