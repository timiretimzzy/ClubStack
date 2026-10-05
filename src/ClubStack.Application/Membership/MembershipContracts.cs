using ClubStack.Domain.Membership;

namespace ClubStack.Application.Membership;

public sealed record PersonDto(
    Guid Id,
    string LegalName,
    string PreferredName,
    Guid? MemberId,
    string? MemberNumber,
    string? IdNumber,
    DateTimeOffset? DateOfBirth,
    string? Email,
    string? Phone,
    string AddressLine1,
    string AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string Notes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreatePersonInput(
    string LegalName,
    string PreferredName,
    string? IdNumber,
    DateTimeOffset? DateOfBirth,
    string? Email,
    string? Phone,
    string AddressLine1,
    string AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string Notes);

/// <summary>Person creation result with likely-duplicate warnings (Phase 2 MEM-003).</summary>
public sealed record PersonCreationResult(PersonDto Person, IReadOnlyList<string> DuplicateWarnings);

public sealed record PersonSearchInput(string? Query = null, int Page = 1, int PageSize = 50);

public sealed record PersonSearchResult(IReadOnlyList<PersonDto> Items, int TotalCount, int Page, int PageSize);

public sealed record MembershipCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string EligibilityRules,
    int RenewalPeriodMonths,
    string PrivilegeConfiguration,
    string GuestRules,
    string AccountRules,
    string CredentialRules,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MembershipCategoryInput(
    string Code,
    string Name,
    string Description,
    string EligibilityRules,
    int RenewalPeriodMonths,
    string PrivilegeConfiguration,
    string GuestRules,
    string AccountRules,
    string CredentialRules,
    bool IsActive);

public sealed record MembershipFeeDto(
    Guid Id,
    Guid CategoryId,
    string FeeType,
    string Description,
    decimal Amount,
    string CurrencyCode,
    bool IsProrationEnabled,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc);

public sealed record MembershipFeeInput(
    string FeeType,
    string Description,
    decimal Amount,
    string CurrencyCode,
    bool IsProrationEnabled,
    bool IsActive);

public sealed record MembershipApplicationDto(
    Guid Id,
    Guid PersonId,
    string PersonName,
    Guid CategoryId,
    string CategoryName,
    string Status,
    string? ApprovedByUserId,
    string? SponsorName,
    string? ReferrerName,
    string Notes,
    string RequiredDocuments,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    string? RejectionReason,
    Guid? MemberId,
    string? MemberNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateMembershipApplicationInput(
    Guid PersonId,
    Guid CategoryId,
    string? SponsorName,
    string? ReferrerName,
    string Notes,
    string RequiredDocuments);

public sealed record RejectMembershipApplicationInput(string Reason);

public sealed record MembershipApplicationSearchInput(string? Status = null, int Page = 1, int PageSize = 50);

public sealed record MembershipApplicationSearchResult(
    IReadOnlyList<MembershipApplicationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record MemberDto(
    Guid Id,
    Guid PersonId,
    string LegalName,
    string PreferredName,
    string MemberNumber,
    string Status,
    string? Phone,
    string? Email);

public sealed record MemberSearchInput(string? Query = null, string? Status = null, int Page = 1, int PageSize = 50);

public sealed record MemberSearchResult(IReadOnlyList<MemberDto> Items, int TotalCount, int Page, int PageSize);

public sealed record MemberDetailDto(
    Guid Id,
    Guid PersonId,
    string LegalName,
    string PreferredName,
    string MemberNumber,
    MemberStatus Status,
    PersonDto Person,
    IReadOnlyList<MembershipDto> Memberships,
    IReadOnlyList<MembershipDependantDto> Dependants);

public sealed record MembershipDto(
    Guid Id,
    Guid MemberId,
    Guid CategoryId,
    string CategoryName,
    MembershipStatus Status,
    DateTimeOffset? StartDateUtc,
    DateTimeOffset? EndDateUtc,
    DateTimeOffset RenewalDateUtc,
    string? ApprovedByUserId,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record MembershipStatusHistoryDto(
    Guid Id,
    Guid MembershipId,
    string PreviousState,
    string NewState,
    string ChangedByUserId,
    string Reason,
    DateTimeOffset ChangedAtUtc);

public sealed record MembershipDependantDto(
    Guid Id,
    Guid MemberId,
    Guid DependantPersonId,
    string DependantName,
    string RelationType,
    string Notes);

public sealed record AddDependantInput(Guid DependantPersonId, string RelationType, string Notes);

public sealed record HouseholdMemberDto(Guid HouseholdId, Guid PersonId, string PersonName, string? MemberNumber, string Role);

public sealed record HouseholdDto(
    Guid Id,
    string Name,
    string Reference,
    Guid? PrimaryMemberId,
    string? PrimaryMemberName,
    string? PrimaryMemberNumber,
    bool IsActive,
    IReadOnlyList<HouseholdMemberDto> Members);

public sealed record HouseholdInput(string Name, Guid? PrimaryMemberId);

public sealed record AddHouseholdMemberInput(Guid PersonId, string Role);

public sealed record MembershipFeeAssessmentDto(
    Guid Id,
    Guid MembershipId,
    string MembershipNumber,
    string MemberName,
    string FeeType,
    string Description,
    decimal Amount,
    string CurrencyCode,
    bool IsProrated,
    DateTimeOffset AssessmentDateUtc,
    DateTimeOffset? DueDateUtc,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record AssessMembershipFeeInput(Guid MembershipId, string FeeType, DateTimeOffset? AssessmentDateUtc);

public sealed record MembershipFeeAdjustmentInput(decimal Amount, string Reason);

public sealed record MembershipFeeVoidInput(string Reason);

public sealed record MembershipFeeAssessmentSearchInput(Guid? MembershipId = null, string? Status = null, int Page = 1, int PageSize = 50);

public sealed record MembershipFeeAssessmentSearchResult(
    IReadOnlyList<MembershipFeeAssessmentDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Membership directory: persons, members, member search, dependants and
/// households (Phase 2 MEM-001/002/003/005/006/010/011/012/014).
/// </summary>
public interface IMembershipDirectoryService
{
    Task<PersonCreationResult> CreatePersonAsync(CreatePersonInput input, CancellationToken cancellationToken = default);

    Task<PersonDto> GetPersonAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PersonSearchResult> SearchPersonsAsync(PersonSearchInput input, CancellationToken cancellationToken = default);

    Task<MemberSearchResult> SearchMembersAsync(MemberSearchInput input, CancellationToken cancellationToken = default);

    Task<MemberDetailDto> GetMemberAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipDependantDto>> ListDependantsAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task<MembershipDependantDto> AddDependantAsync(Guid memberId, AddDependantInput input, CancellationToken cancellationToken = default);

    Task RemoveDependantAsync(Guid memberId, Guid dependantPersonId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HouseholdDto>> ListHouseholdsAsync(CancellationToken cancellationToken = default);

    Task<HouseholdDto> GetHouseholdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<HouseholdDto> CreateHouseholdAsync(HouseholdInput input, CancellationToken cancellationToken = default);

    Task<HouseholdDto> UpdateHouseholdAsync(Guid id, HouseholdInput input, CancellationToken cancellationToken = default);

    Task<HouseholdDto> AddHouseholdMemberAsync(Guid id, AddHouseholdMemberInput input, CancellationToken cancellationToken = default);

    Task RemoveHouseholdMemberAsync(Guid householdId, Guid personId, CancellationToken cancellationToken = default);
}

/// <summary>Membership categories and their configurable fee definitions (Phase 2 MEM-021/022/040).</summary>
public interface IMembershipCategoryService
{
    Task<IReadOnlyList<MembershipCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<MembershipCategoryDto> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MembershipCategoryDto> CreateCategoryAsync(MembershipCategoryInput input, CancellationToken cancellationToken = default);

    Task<MembershipCategoryDto> UpdateCategoryAsync(Guid id, MembershipCategoryInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipFeeDto>> GetCategoryFeesAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<MembershipFeeDto> CreateFeeAsync(Guid categoryId, MembershipFeeInput input, CancellationToken cancellationToken = default);

    Task<MembershipFeeDto> UpdateFeeAsync(Guid categoryId, Guid feeId, MembershipFeeInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Membership lifecycle: applications through approval, activation, suspension,
/// reinstatement, expiry, renewal, cancellation and transfer (Phase 8 §7.2/§7.3,
/// Phase 2 MEM-020/023..032, MEM-028/029). Every transition is validated against
/// the approved state machines and recorded in membership status history.
/// </summary>
public interface IMembershipLifecycleService
{
    Task<MembershipApplicationDto> CreateApplicationAsync(CreateMembershipApplicationInput input, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDto> GetApplicationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MembershipApplicationSearchResult> SearchApplicationsAsync(MembershipApplicationSearchInput input, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDto> SubmitApplicationAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDto> StartReviewApplicationAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDto> ApproveApplicationAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipApplicationDto> RejectApplicationAsync(Guid id, RejectMembershipApplicationInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipDto>> GetMembershipsForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task<MembershipDto> GetMembershipAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipStatusHistoryDto>> GetMembershipHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MembershipDto> ActivateMembershipAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipDto> SuspendMembershipAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipDto> ReinstateMembershipAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipDto> RenewMembershipAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipDto> CancelMembershipAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipDto> TransferMembershipAsync(Guid id, Guid targetCategoryId, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Traceable membership fee assessments (Phase 2 MEM-041/042/043/044).</summary>
public interface IMembershipFeeService
{
    Task<MembershipFeeAssessmentSearchResult> SearchAssessmentsAsync(MembershipFeeAssessmentSearchInput input, CancellationToken cancellationToken = default);

    Task<MembershipFeeAssessmentDto> GetAssessmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MembershipFeeAssessmentDto> AssessAsync(AssessMembershipFeeInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipFeeAssessmentDto> AdjustAsync(Guid id, MembershipFeeAdjustmentInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<MembershipFeeAssessmentDto> VoidAsync(Guid id, MembershipFeeVoidInput input, string actorUserId, CancellationToken cancellationToken = default);
}