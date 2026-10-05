using ClubStack.Application.Exceptions;
using ClubStack.Application.Membership;
using ClubStack.Domain.Common;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Membership;

/// <inheritdoc />
public sealed class EntityFrameworkMembershipFeeService : IMembershipFeeService
{
    private const int DefaultPaymentTermsDays = 30;

    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;
    private readonly IFinanceRequirementWriter _financeRequirements;

    public EntityFrameworkMembershipFeeService(
        ClubStackDbContext context,
        ISystemClock clock,
        IFinanceRequirementWriter financeRequirements)
    {
        _context = context;
        _clock = clock;
        _financeRequirements = financeRequirements;
    }

    public async Task<MembershipFeeAssessmentSearchResult> SearchAssessmentsAsync(
        MembershipFeeAssessmentSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePage(input.Page, input.PageSize);
        IQueryable<MembershipFeeAssessmentEntity> query = _context.MembershipFeeAssessments
            .Include(a => a.Membership).ThenInclude(m => m.Member).ThenInclude(m => m.Person)
            .AsNoTracking();

        if (input.MembershipId is not null)
        {
            query = query.Where(a => a.MembershipId == input.MembershipId);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = MembershipServiceHelpers.ParseEnum<MembershipFeeAssessmentStatus>(input.Status, "status");
            query = query.Where(a => a.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.AssessmentDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MembershipFeeAssessmentSearchResult(
            items.Select(ToAssessmentDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<MembershipFeeAssessmentDto> GetAssessmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var assessment = await _context.MembershipFeeAssessments
            .Include(a => a.Membership).ThenInclude(m => m.Member).ThenInclude(m => m.Person)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Fee assessment '{id}' was not found.");

        return ToAssessmentDto(assessment);
    }

    public async Task<MembershipFeeAssessmentDto> AssessAsync(
        AssessMembershipFeeInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.Memberships
            .Include(m => m.Member)
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == input.MembershipId, cancellationToken)
            ?? throw new NotFoundException($"Membership '{input.MembershipId}' was not found.");

        await MembershipServiceHelpers.ExpireActiveOverdueMembershipsAsync(_context, _clock, membership.MemberId, cancellationToken);

        if (membership.Status != MembershipStatus.Active && membership.Status != MembershipStatus.Expired)
        {
            throw new ConflictException(
                $"Fees can only be assessed for an active or expired membership, not '{membership.Status}'.");
        }

        var feeType = MembershipServiceHelpers.ParseEnum<MembershipFeeType>(input.FeeType, "feeType");

        var fee = await _context.MembershipFees.AsNoTracking()
            .FirstOrDefaultAsync(f => f.CategoryId == membership.CategoryId
                                      && f.FeeType == feeType
                                      && f.IsActive, cancellationToken)
            ?? throw new ValidationException(
                "The fee could not be assessed.",
                MembershipServiceHelpers.FieldError(
                    "feeType",
                    $"No active fee of type '{feeType}' is configured for category '{membership.Category.Name}'."));

        var assessmentDate = input.AssessmentDateUtc ?? _clock.UtcNow;
        var amount = fee.Amount;
        var isProrated = false;

        if (fee.IsProrationEnabled
            && fee.FeeType == MembershipFeeType.AnnualFee
            && membership.StartDateUtc is not null
            && membership.EndDateUtc is not null)
        {
            var periodDays = Math.Max(1, (membership.EndDateUtc.Value - membership.StartDateUtc.Value).Days);
            var remainingDays = Math.Max(0, (membership.EndDateUtc.Value - assessmentDate).Days);
            if (remainingDays < periodDays)
            {
                var prorated = FeeProration.Apply(fee.Amount, periodDays, remainingDays);
                if (prorated != fee.Amount)
                {
                    amount = prorated;
                    isProrated = true;
                }
            }
        }

        var assessmentId = Guid.CreateVersion7();
        var assessment = new MembershipFeeAssessmentEntity
        {
            Id = assessmentId,
            MembershipId = membership.Id,
            FeeType = feeType,
            Description = fee.Description,
            Amount = amount,
            CurrencyCode = fee.CurrencyCode,
            IsProrated = isProrated,
            AssessmentDateUtc = assessmentDate,
            DueDateUtc = assessmentDate.AddDays(DefaultPaymentTermsDays),
            Source = string.IsNullOrWhiteSpace(fee.Description)
                ? $"Assessed from membership fee configuration ({membership.Category.Name})."
                : $"Assessed from {fee.Description} ({membership.Category.Name}).",
        };

        _context.MembershipFeeAssessments.Add(assessment);

        // Phase 8 §14.4: the assessment owes Dr member receivable / Cr membership revenue.
        // The outbox message is written in the same unit of work as the assessment, so the
        // operational record and its financial statement commit atomically.
        var memberAccountId = await _context.MemberAccounts
            .AsNoTracking()
            .Where(a => a.MemberId == membership.MemberId)
            .OrderBy(a => a.OpenedAtUtc)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        await _financeRequirements.RequireMembershipFeePostingAsync(
            assessment.Id,
            membership.Id,
            membership.MemberId,
            memberAccountId,
            assessment.Source,
            assessment.Description,
            amount,
            assessment.CurrencyCode,
            assessment.AssessmentDateUtc,
            assessment.AssessmentDateUtc,
            string.Empty,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetAssessmentAsync(assessmentId, cancellationToken);
    }

    public async Task<MembershipFeeAssessmentDto> AdjustAsync(
        Guid id,
        MembershipFeeAdjustmentInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _context.MembershipFeeAssessments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                         ?? throw new NotFoundException($"Fee assessment '{id}' was not found.");

        if (assessment.Status != MembershipFeeAssessmentStatus.Outstanding)
        {
            throw new ConflictException($"A fee assessment in state '{assessment.Status}' cannot be adjusted.");
        }

        if (input.Amount <= 0)
        {
            throw new ValidationException(
                "The fee assessment could not be adjusted.",
                MembershipServiceHelpers.FieldError("amount", "The adjusted amount must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The fee assessment could not be adjusted.",
                MembershipServiceHelpers.FieldError("reason", "A reason is required."));
        }

        assessment.Amount = input.Amount;
        assessment.AdjustmentReason = input.Reason.Trim();
        assessment.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetAssessmentAsync(id, cancellationToken);
    }

    public async Task<MembershipFeeAssessmentDto> VoidAsync(
        Guid id,
        MembershipFeeVoidInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _context.MembershipFeeAssessments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                         ?? throw new NotFoundException($"Fee assessment '{id}' was not found.");

        if (assessment.Status != MembershipFeeAssessmentStatus.Outstanding)
        {
            throw new ConflictException($"A fee assessment in state '{assessment.Status}' cannot be voided.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The fee assessment could not be voided.",
                MembershipServiceHelpers.FieldError("reason", "A reason is required."));
        }

        assessment.Status = MembershipFeeAssessmentStatus.Voided;
        assessment.VoidReason = input.Reason.Trim();
        assessment.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetAssessmentAsync(id, cancellationToken);
    }

    private static MembershipFeeAssessmentDto ToAssessmentDto(MembershipFeeAssessmentEntity a) => new(
        a.Id,
        a.MembershipId,
        a.Membership.Member.MemberNumber,
        a.Membership.Member.Person.LegalName,
        MembershipServiceHelpers.ToName(a.FeeType),
        a.Description,
        a.Amount,
        a.CurrencyCode,
        a.IsProrated,
        a.AssessmentDateUtc,
        a.DueDateUtc,
        a.Source,
        MembershipServiceHelpers.ToName(a.Status),
        a.CreatedAtUtc);

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);
        return (normalizedPage, normalizedSize);
    }
}