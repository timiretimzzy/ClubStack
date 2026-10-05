using ClubStack.Domain.Membership;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// Person identity record (Phase 8 §8.3 <c>persons</c>, Phase 3 §6/§7). Persons are
/// separate from memberships and credentials; a person can exist without a membership.
/// Identification details are restricted/sensitive operational data.
/// </summary>
public sealed class PersonEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string LegalName { get; set; } = string.Empty;

    public string PreferredName { get; set; } = string.Empty;

    public string? IdNumber { get; set; }

    public DateTimeOffset? DateOfBirth { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string AddressLine2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public MemberEntity? Member { get; set; }
}

/// <summary>
/// Member registry record (Phase 8 §8.3 <c>members</c>, Phase 8 §7.1). Carries the
/// unique, server-assigned member number and the member lifecycle status.
/// </summary>
public sealed class MemberEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PersonId { get; set; }

    public PersonEntity Person { get; set; } = null!;

    public string MemberNumber { get; set; } = string.Empty;

    public MemberStatus Status { get; set; } = MemberStatus.Prospect;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MembershipEntity> Memberships { get; set; } = new List<MembershipEntity>();
}

/// <summary>
/// Membership category configuration (Phase 8 §8.3 <c>membership_categories</c>,
/// Phase 3 §10, Phase 2 MEM-021/MEM-022). Category values are club configuration.
/// </summary>
public sealed class MembershipCategoryEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EligibilityRules { get; set; } = string.Empty;

    public int RenewalPeriodMonths { get; set; } = 12;

    public string PrivilegeConfiguration { get; set; } = string.Empty;

    public string GuestRules { get; set; } = string.Empty;

    public string AccountRules { get; set; } = string.Empty;

    public string CredentialRules { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MembershipEntity> Memberships { get; set; } = new List<MembershipEntity>();

    public ICollection<MembershipFeeEntity> Fees { get; set; } = new List<MembershipFeeEntity>();
}

/// <summary>
/// Membership application (Phase 8 §8.3 <c>membership_applications</c>, Phase 8 §7.2).
/// Approval is a controlled action; an approved application completes when the
/// resulting membership is activated.
/// </summary>
public sealed class MembershipApplicationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PersonId { get; set; }

    public PersonEntity Person { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public MembershipCategoryEntity Category { get; set; } = null!;

    public MembershipApplicationStatus Status { get; set; } = MembershipApplicationStatus.Draft;

    public string? SponsorName { get; set; }

    public string? ReferrerName { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string RequiredDocuments { get; set; } = string.Empty;

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public string? ApproverUserId { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? MemberId { get; set; }

    public MemberEntity? Member { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One membership period for a member (Phase 8 §8.3 <c>memberships</c>, Phase 3 §11).
/// The record survives beyond its active period; history is never destroyed.
/// </summary>
public sealed class MembershipEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MemberId { get; set; }

    public MemberEntity Member { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public MembershipCategoryEntity Category { get; set; } = null!;

    public MembershipStatus Status { get; set; } = MembershipStatus.Pending;

    public DateTimeOffset? StartDateUtc { get; set; }

    public DateTimeOffset? EndDateUtc { get; set; }

    public DateTimeOffset RenewalDateUtc { get; set; }

    public Guid? PreviousMembershipId { get; set; }

    public MembershipEntity? PreviousMembership { get; set; }

    public string? ApprovedByUserId { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset? ActivatedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MembershipStatusHistoryEntity> StatusHistory { get; set; } = new List<MembershipStatusHistoryEntity>();

    public ICollection<MembershipFeeAssessmentEntity> FeeAssessments { get; set; } = new List<MembershipFeeAssessmentEntity>();
}

/// <summary>
/// Membership status transition evidence (Phase 8 §8.3 <c>membership_status_history</c>,
/// Phase 3 §13). Lifecycle history is never reconstructed from audit logs alone.
/// </summary>
public sealed class MembershipStatusHistoryEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MembershipId { get; set; }

    public MembershipEntity Membership { get; set; } = null!;

    public MembershipStatus PreviousState { get; set; }

    public MembershipStatus NewState { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Household grouping of related persons (Phase 8 §8.3 <c>households</c>, Phase 3 §8).
/// Owned by the membership domain.
/// </summary>
public sealed class HouseholdEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public Guid? PrimaryMemberId { get; set; }

    public MemberEntity? PrimaryMember { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<HouseholdMemberEntity> Members { get; set; } = new List<HouseholdMemberEntity>();
}

/// <summary>
/// Household membership relationship (Phase 8 §8.3 <c>household_members</c>, Phase 3 §9).
/// The relationship role is stored, never inferred from names, and historical
/// relationships remain traceable.
/// </summary>
public sealed class HouseholdMemberEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public HouseholdEntity Household { get; set; } = null!;

    public Guid PersonId { get; set; }

    public PersonEntity Person { get; set; } = null!;

    public HouseholdMemberRole Role { get; set; } = HouseholdMemberRole.Dependant;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dependant relationship for a member (Phase 8 §8.3 <c>membership_dependants</c>,
/// Phase 2 MEM-012). Dependants are distinct people/records whose privileges are
/// determined by membership configuration, never blanket-inherited (MEM-013).
/// </summary>
public sealed class MembershipDependantEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MemberId { get; set; }

    public MemberEntity Member { get; set; } = null!;

    public Guid DependantPersonId { get; set; }

    public PersonEntity DependantPerson { get; set; } = null!;

    public DependantRelationType RelationType { get; set; } = DependantRelationType.Child;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configurable membership fee definition (Phase 8 §8.3 <c>membership_fees</c>,
/// Phase 2 MEM-040/MEM-043/MEM-044). Fees are configured per membership category
/// and fee type; proration is enabled per definition.
/// </summary>
public sealed class MembershipFeeEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CategoryId { get; set; }

    public MembershipCategoryEntity Category { get; set; } = null!;

    public MembershipFeeType FeeType { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "ZAR";

    public bool IsProrationEnabled { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Traceable membership fee assessment (Phase 8 §8.3 <c>membership_fee_assessments</c>,
/// Phase 3 §14, Phase 2 MEM-041/MEM-042). An assessment is a financial charge
/// associated with a membership; ledger settlement happens in a later slice.
/// </summary>
public sealed class MembershipFeeAssessmentEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MembershipId { get; set; }

    public MembershipEntity Membership { get; set; } = null!;

    public MembershipFeeType FeeType { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "ZAR";

    public bool IsProrated { get; set; }

    public DateTimeOffset AssessmentDateUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueDateUtc { get; set; }

    public string Source { get; set; } = string.Empty;

    public MembershipFeeAssessmentStatus Status { get; set; } = MembershipFeeAssessmentStatus.Outstanding;

    public string? AdjustmentReason { get; set; }

    public string? VoidReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}