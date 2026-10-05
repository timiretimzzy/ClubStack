using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Finance;

/// <summary>
/// Accounts receivable (B13, §9). Member identity stays the existing
/// Member/MemberAccount: a receivable is the finance-side view of an amount owed by
/// a member, never a second member master. Documents are created either alongside a
/// journal the engine already posted, or by asking the engine to post one with a
/// caller-chosen configured posting rule.
/// </summary>
public sealed class EntityFrameworkReceivableService : IReceivableService
{
    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly IFinancePostingService _posting;
    private readonly ISystemClock _clock;

    public EntityFrameworkReceivableService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        IFinancePostingService posting,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _posting = posting;
        _clock = clock;
    }

    public async Task<ReceivableSearchResult> SearchAsync(
        ReceivableSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<AccountsReceivableEntity> query = _context.AccountsReceivable.FullReceivables().AsNoTracking();

        if (input.MemberId is { } memberId)
        {
            query = query.Where(a => a.MemberId == memberId);
        }

        if (input.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }
        else
        {
            // Default view excludes cancelled documents.
            query = query.Where(a => a.Status != ReceivableStatus.Cancelled);
        }

        if (input.AsOf is { } asOf)
        {
            query = query.Where(a => a.TransactionDate <= asOf);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.TransactionDate)
            .ThenBy(a => a.Reference)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalOutstanding = FinanceDomain.NormalizeAmount(
            await query.SumAsync(a => (decimal?)a.OutstandingAmount, cancellationToken) ?? 0m);

        return new ReceivableSearchResult(
            items.Select(ToDto).ToList(),
            total,
            totalOutstanding,
            page,
            pageSize);
    }

    public async Task<ReceivableDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var receivable = await _context.AccountsReceivable
            .FullReceivables()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Receivable '{id}' was not found.");

        return ToDto(receivable);
    }

    public async Task<ReceivableDto?> GetBySourceAsync(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var receivable = await _context.AccountsReceivable
            .FullReceivables()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.SourceType == sourceType && a.SourceId == sourceId, cancellationToken);

        return receivable is null ? null : ToDto(receivable);
    }

    public async Task<ReceivableDto> CreateAsync(
        CreateReceivableInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        // Phase 8 §15: the same source transaction cannot produce duplicate receivables.
        var existing = await _context.AccountsReceivable
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.SourceType == input.SourceType && a.SourceId == input.SourceId,
                cancellationToken);

        if (existing is not null)
        {
            return await GetAsync(existing.Id, cancellationToken);
        }

        var amount = FinanceDomain.NormalizeAmount(input.Amount);
        if (amount <= 0m)
        {
            throw new ValidationException(
                "The receivable could not be created.",
                FinanceServiceHelpers.FieldError("amount", "A receivable amount must be greater than zero."));
        }

        if (!FinanceDomain.FitsAmountPrecision(amount))
        {
            throw new ValidationException(
                "The receivable could not be created.",
                FinanceServiceHelpers.FieldError("amount", "The amount exceeds the supported decimal(19,4) precision."));
        }

        var member = await _context.Members
            .Include(m => m.Person)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == input.MemberId, cancellationToken)
            ?? throw new ValidationException(
                "The receivable could not be created.",
                FinanceServiceHelpers.FieldError("memberId", $"Member '{input.MemberId}' does not exist."));

        var currency = FinanceDomain.NormalizeCurrency(input.CurrencyCode);
        await FinanceServiceHelpers.RequireKnownCurrencyAsync(_context, currency, cancellationToken);

        var memberAccountId = await _context.MemberAccounts
            .AsNoTracking()
            .Where(a => a.MemberId == input.MemberId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // The journal is posted first so the receivable can link to it. When the caller
        // supplies a journal, the posting already happened in the same unit of work.
        Guid journalId;
        if (input.JournalId is { } suppliedJournalId)
        {
            journalId = suppliedJournalId;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.RuleCode))
            {
                throw new ValidationException(
                    "The receivable could not be created.",
                    FinanceServiceHelpers.FieldError("ruleCode",
                        "A posting rule is required to create a receivable with its journal. " +
                        "Supply one of the configured Phase 8 §14 rules, or pass an already-posted journal."));
            }

            var journal = await _posting.PostAsync(
                new FinancialPostingRequest(
                    input.RuleCode,
                    input.SourceType,
                    input.SourceId,
                    input.SourceReference,
                    input.TransactionDate,
                    input.TransactionDate,
                    currency,
                    input.ExchangeRate,
                    input.RateDateUtc,
                    FinanceServiceHelpers.NormalizeOptional(input.Description) ?? $"Receivable for {member.Person.LegalName}",
                    input.Amounts ?? [new FinancialPostingAmount(null, amount)]),
                actorUserId,
                string.Empty,
                cancellationToken);

            journalId = journal.Id;
        }

        var receivable = new AccountsReceivableEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(
                ReceivableReference.Prefix,
                ReceivableReference.Width,
                cancellationToken),
            MemberId = input.MemberId,
            MemberAccountId = memberAccountId,
            SourceType = input.SourceType,
            SourceId = input.SourceId,
            SourceReference = FinanceServiceHelpers.NormalizeOptional(input.SourceReference),
            OriginalAmount = amount,
            AllocatedAmount = 0m,
            OutstandingAmount = amount,
            TransactionDate = input.TransactionDate,
            DueDate = input.DueDate,
            CurrencyCode = currency,
            Status = ReceivableStatus.Open,
            JournalId = journalId,
            Description = FinanceServiceHelpers.NormalizeOptional(input.Description),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        _context.AccountsReceivable.Add(receivable);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetAsync(receivable.Id, cancellationToken);
    }

    public async Task<ReceivableDto> CancelAsync(
        Guid id,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var receivable = await _context.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Receivable '{id}' was not found.");

        if (receivable.Status == ReceivableStatus.Cancelled)
        {
            return await GetAsync(id, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                "The receivable could not be cancelled.",
                FinanceServiceHelpers.FieldError("reason", "A reason is required to cancel a receivable."));
        }

        if (receivable.AllocatedAmount > 0m)
        {
            // Cancelling a partly settled document would leave the allocated payments
            // pointing at a cancelled document; the payments must be unwound first.
            throw new ConflictException(
                $"Receivable '{receivable.Reference}' has {receivable.AllocatedAmount} allocated and cannot be cancelled. " +
                "Reverse the related payment allocations first.");
        }

        receivable.Status = ReceivableStatus.Cancelled;
        receivable.OutstandingAmount = 0m;
        receivable.CancelledReason = reason.Trim();
        receivable.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Applies a payment allocation to a receivable and moves its B13 status. The
    /// caller owns the transaction, so an allocation and the payment that carries it
    /// commit together.
    /// </summary>
    internal static void ApplyAllocation(
        AccountsReceivableEntity receivable,
        decimal amount,
        string actorUserId,
        DateTimeOffset now)
    {
        if (amount <= 0m)
        {
            throw new ValidationException(
                "The allocation could not be applied.",
                FinanceServiceHelpers.FieldError("amount", "An allocation amount must be greater than zero."));
        }

        if (amount > receivable.OutstandingAmount)
        {
            throw new ValidationException(
                "The allocation could not be applied.",
                FinanceServiceHelpers.FieldError("amount",
                    $"The allocation exceeds the outstanding amount of {receivable.OutstandingAmount} on '{receivable.Reference}'."));
        }

        receivable.AllocatedAmount = FinanceDomain.NormalizeAmount(receivable.AllocatedAmount + amount);
        receivable.OutstandingAmount = FinanceDomain.NormalizeAmount(receivable.OriginalAmount - receivable.AllocatedAmount);
        receivable.Status = receivable.OutstandingAmount <= 0m
            ? ReceivableStatus.Paid
            : ReceivableStatus.PartiallyPaid;
        receivable.UpdatedAtUtc = now;

        _ = actorUserId;
    }

    internal static ReceivableDto ToDto(AccountsReceivableEntity a) => new(
        a.Id,
        a.Reference,
        a.MemberId,
        a.Member.MemberNumber,
        a.Member.Person.LegalName,
        a.MemberAccountId,
        a.SourceType,
        a.SourceId,
        a.SourceReference,
        a.OriginalAmount,
        a.AllocatedAmount,
        a.OutstandingAmount,
        a.TransactionDate,
        a.DueDate,
        a.CurrencyCode,
        a.Status,
        a.JournalId,
        a.Journal?.Reference,
        a.Description,
        a.CancelledReason ?? string.Empty,
        a.CreatedAtUtc,
        a.Allocations
            .OrderBy(x => x.AllocatedAtUtc)
            .Select(x => new ReceivableAllocationDto(
                x.Id,
                x.PaymentId,
                x.Payment.Reference,
                x.Amount,
                x.AllocatedAtUtc,
                x.AllocatedByUserId))
            .ToList());
}
