namespace ClubStack.Domain.Membership;

/// <summary>
/// Membership application lifecycle states (Phase 8 §7.2).
/// Approved applications complete as <see cref="Activated"/> when the resulting
/// membership is activated; rejected applications cannot be activated without a
/// new approved workflow.
/// </summary>
public enum MembershipApplicationStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    Activated,
}

/// <summary>
/// Member (registry) lifecycle states (Phase 8 §7.1). The member record is the
/// durable person-to-club relationship carrier with the unique member number.
/// </summary>
public enum MemberStatus
{
    Prospect,
    Active,
    Suspended,
    Cancelled,
}

/// <summary>
/// Membership (period) lifecycle states (Phase 8 §7.3, Phase 3 §12). A member may
/// carry multiple historical membership periods; the record survives its active
/// period. Transfer produces a controlled historical transition
/// (<see cref="Transferred"/>).
/// </summary>
public enum MembershipStatus
{
    Pending,
    Active,
    Suspended,
    Expired,
    Cancelled,
    Transferred,
}

/// <summary>
/// Fee categories a club can configure per membership category (Phase 3 §14,
/// Phase 2 MEM-040/MEM-044). Renewal fees remain distinguishable from unrelated
/// member charges.
/// </summary>
public enum MembershipFeeType
{
    JoiningFee,
    AnnualFee,
    RenewalFee,
}

/// <summary>
/// Financial lifecycle of a membership fee assessment (Phase 3 §14). Assessments
/// are traceable financial charges; ledger settlement is a later (D3+) concern.
/// </summary>
public enum MembershipFeeAssessmentStatus
{
    Outstanding,
    Adjusted,
    Voided,
}

/// <summary>Household membership role (Phase 3 §9). Stored, never inferred from names.</summary>
public enum HouseholdMemberRole
{
    Primary,
    SpousePartner,
    Dependant,
}

/// <summary>Dependant relationship to the member (Phase 2 MEM-012).</summary>
public enum DependantRelationType
{
    Spouse,
    Partner,
    Child,
    Parent,
    Other,
}