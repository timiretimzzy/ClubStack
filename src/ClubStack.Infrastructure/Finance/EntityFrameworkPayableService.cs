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
/// Accounts payable (B13: "AP mirrors AR for suppliers", §9). Supplier identity
/// stays the existing Supplier. A payable document is created either alongside a
/// journal the engine already posted, or by asking the engine to post one with a
/// caller-chosen configured posting rule.
/// </summary>
public sealed class EntityFrameworkPayableService : IPayableService
{
    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly IFinancePostingService _posting;
    private readonly ISystemClock _clock;

    public EntityFrameworkPayableService(
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

    public async Task<PayableSearchResult> SearchAsync(
        PayableSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<AccountsPayableEntity> query = _context.AccountsPayable.FullPayables().AsNoTracking();

        if (input.SupplierId is { } supplierId)
        {
            query = query.Where(a => a.SupplierId == supplierId);
        }

        if (input.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }
        else
        {
            query = query.Where(a => a.Status != PayableStatus.Cancelled);
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

        return new PayableSearchResult(
            items.Select(ToDto).ToList(),
            total,
            totalOutstanding,
            page,
            pageSize);
    }

    public async Task<PayableDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payable = await _context.AccountsPayable
            .FullPayables()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Payable '{id}' was not found.");

        return ToDto(payable);
    }

    public async Task<PayableDto?> GetBySourceAsync(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var payable = await _context.AccountsPayable
            .FullPayables()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.SourceType == sourceType && a.SourceId == sourceId, cancellationToken);

        return payable is null ? null : ToDto(payable);
    }

    public async Task<PayableDto> CreateAsync(
        CreatePayableInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AccountsPayable
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
                "The payable could not be created.",
                FinanceServiceHelpers.FieldError("amount", "A payable amount must be greater than zero."));
        }

        if (!FinanceDomain.FitsAmountPrecision(amount))
        {
            throw new ValidationException(
                "The payable could not be created.",
                FinanceServiceHelpers.FieldError("amount", "The amount exceeds the supported decimal(19,4) precision."));
        }

        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == input.SupplierId, cancellationToken)
            ?? throw new ValidationException(
                "The payable could not be created.",
                FinanceServiceHelpers.FieldError("supplierId", $"Supplier '{input.SupplierId}' does not exist."));

        var currency = FinanceDomain.NormalizeCurrency(input.CurrencyCode);
        await FinanceServiceHelpers.RequireKnownCurrencyAsync(_context, currency, cancellationToken);

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
                    "The payable could not be created.",
                    FinanceServiceHelpers.FieldError("ruleCode",
                        "A posting rule is required to create a payable with its journal. " +
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
                    FinanceServiceHelpers.NormalizeOptional(input.Description) ?? $"Payable for {supplier.Name}",
                    input.Amounts ?? [new FinancialPostingAmount(null, amount)]),
                actorUserId,
                string.Empty,
                cancellationToken);

            journalId = journal.Id;
        }

        var payable = new AccountsPayableEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(
                PayableReference.Prefix,
                PayableReference.Width,
                cancellationToken),
            SupplierId = input.SupplierId,
            SourceType = input.SourceType,
            SourceId = input.SourceId,
            SourceReference = FinanceServiceHelpers.NormalizeOptional(input.SourceReference),
            OriginalAmount = amount,
            AllocatedAmount = 0m,
            OutstandingAmount = amount,
            TransactionDate = input.TransactionDate,
            DueDate = input.DueDate,
            CurrencyCode = currency,
            Status = PayableStatus.Open,
            JournalId = journalId,
            Description = FinanceServiceHelpers.NormalizeOptional(input.Description),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        _context.AccountsPayable.Add(payable);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetAsync(payable.Id, cancellationToken);
    }

    public async Task<PayableDto> CancelAsync(
        Guid id,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var payable = await _context.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Payable '{id}' was not found.");

        if (payable.Status == PayableStatus.Cancelled)
        {
            return await GetAsync(id, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                "The payable could not be cancelled.",
                FinanceServiceHelpers.FieldError("reason", "A reason is required to cancel a payable."));
        }

        if (payable.AllocatedAmount > 0m)
        {
            throw new ConflictException(
                $"Payable '{payable.Reference}' has {payable.AllocatedAmount} allocated and cannot be cancelled. " +
                "Reverse the related payment allocations first.");
        }

        payable.Status = PayableStatus.Cancelled;
        payable.OutstandingAmount = 0m;
        payable.CancelledReason = reason.Trim();
        payable.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Applies a payment allocation to a payable and moves its B13 status.</summary>
    internal static void ApplyAllocation(
        AccountsPayableEntity payable,
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

        if (amount > payable.OutstandingAmount)
        {
            throw new ValidationException(
                "The allocation could not be applied.",
                FinanceServiceHelpers.FieldError("amount",
                    $"The allocation exceeds the outstanding amount of {payable.OutstandingAmount} on '{payable.Reference}'."));
        }

        payable.AllocatedAmount = FinanceDomain.NormalizeAmount(payable.AllocatedAmount + amount);
        payable.OutstandingAmount = FinanceDomain.NormalizeAmount(payable.OriginalAmount - payable.AllocatedAmount);
        payable.Status = payable.OutstandingAmount <= 0m
            ? PayableStatus.Paid
            : PayableStatus.PartiallyPaid;
        payable.UpdatedAtUtc = now;

        _ = actorUserId;
    }

    internal static PayableDto ToDto(AccountsPayableEntity a) => new(
        a.Id,
        a.Reference,
        a.SupplierId,
        a.Supplier.SupplierCode,
        a.Supplier.Name,
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
            .Select(x => new PayableAllocationDto(
                x.Id,
                x.PaymentId,
                x.Payment.Reference,
                x.Amount,
                x.AllocatedAtUtc,
                x.AllocatedByUserId))
            .ToList());
}
