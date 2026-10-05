using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubStack.Infrastructure.Finance;

/// <summary>
/// Finance payments (§10). A payment is the accounting representation of money
/// received from a member or paid to a supplier, and the mechanism that allocates it
/// to receivables/payables. It links back to the operational record
/// (AccountTransactionId / SourceType + SourceId) and never becomes a second POS
/// tender ledger. The payment row, its journal and every allocation are written in
/// one transaction.
/// </summary>
public sealed class EntityFrameworkFinancePaymentService : IFinancePaymentService
{
    private const string UniqueViolation = "23505";

    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly IFinancePostingService _posting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;

    public EntityFrameworkFinancePaymentService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        IFinancePostingService posting,
        IUnitOfWork unitOfWork,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _posting = posting;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<PaymentSearchResult> SearchAsync(
        PaymentSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<PaymentEntity> query = _context.Payments.FullPayments().AsNoTracking();

        if (input.Direction is { } direction)
        {
            query = query.Where(p => p.Direction == direction);
        }

        if (input.MemberId is { } memberId)
        {
            query = query.Where(p => p.MemberId == memberId);
        }

        if (input.SupplierId is { } supplierId)
        {
            query = query.Where(p => p.SupplierId == supplierId);
        }

        if (input.FromDate is { } from)
        {
            query = query.Where(p => p.TransactionDate >= from);
        }

        if (input.ToDate is { } to)
        {
            query = query.Where(p => p.TransactionDate < to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.TransactionDate)
            .ThenBy(p => p.Reference)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaymentSearchResult(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<PaymentDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .FullPayments()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Payment '{id}' was not found.");

        return ToDto(payment);
    }

    public async Task<PaymentDto?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .FullPayments()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

        return payment is null ? null : ToDto(payment);
    }

    public async Task<PaymentDto> RecordAsync(
        RecordPaymentInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var key = FinanceServiceHelpers.NormalizeOptional(input.IdempotencyKey);

        if (key is not null)
        {
            var replay = await GetByIdempotencyKeyAsync(key, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        if (input.SourceId is { } sourceId && !string.IsNullOrWhiteSpace(input.SourceType))
        {
            var existing = await _context.Payments
                .FullPayments()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.SourceType == input.SourceType && p.SourceId == sourceId,
                    cancellationToken);

            if (existing is not null)
            {
                return ToDto(existing);
            }
        }

        var amount = FinanceDomain.NormalizeAmount(input.Amount);
        if (amount <= 0m)
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("amount", "A payment amount must be greater than zero."));
        }

        if (!FinanceDomain.FitsAmountPrecision(amount))
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("amount", "The amount exceeds the supported decimal(19,4) precision."));
        }

        var currency = FinanceDomain.NormalizeCurrency(input.CurrencyCode);
        await FinanceServiceHelpers.RequireKnownCurrencyAsync(_context, currency, cancellationToken);

        // Direction determines the counterparty and the posting rule family.
        if (input.Direction == PaymentDirection.Received && input.MemberId is null)
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("memberId", "A payment received requires a member."));
        }

        if (input.Direction == PaymentDirection.Made && input.SupplierId is null)
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("supplierId", "A payment made requires a supplier."));
        }

        if (input.MemberId is { } memberId && !await _context.Members
                .AsNoTracking()
                .AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("memberId", $"Member '{memberId}' does not exist."));
        }

        if (input.SupplierId is { } supplierId && !await _context.Suppliers
                .AsNoTracking()
                .AnyAsync(s => s.Id == supplierId, cancellationToken))
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("supplierId", $"Supplier '{supplierId}' does not exist."));
        }

        // Validate the requested allocations against live documents before anything is written.
        var allocations = input.Allocations?.ToList() ?? [];
        var normalizedAllocations = new List<(AccountsReceivableEntity? Receivable, AccountsPayableEntity? Payable, decimal Amount)>();
        decimal allocatedTotal = 0m;

        foreach (var allocation in allocations)
        {
            if (allocation.ReceivableId is null && allocation.PayableId is null)
            {
                throw new ValidationException(
                    "The payment could not be recorded.",
                    FinanceServiceHelpers.FieldError("allocations",
                        "Each allocation must target either a receivable or a payable."));
            }

            if (allocation.ReceivableId is not null && allocation.PayableId is not null)
            {
                throw new ValidationException(
                    "The payment could not be recorded.",
                    FinanceServiceHelpers.FieldError("allocations",
                        "An allocation targets either a receivable or a payable, never both."));
            }

            if (allocation.ReceivableId is { } arId)
            {
                var receivable = await _context.AccountsReceivable
                    .FirstOrDefaultAsync(a => a.Id == arId, cancellationToken)
                    ?? throw new ValidationException(
                        "The payment could not be recorded.",
                        FinanceServiceHelpers.FieldError("allocations", $"Receivable '{arId}' does not exist."));

                if (receivable.Status == ReceivableStatus.Cancelled)
                {
                    throw new ConflictException(
                        $"Receivable '{receivable.Reference}' is cancelled and cannot receive an allocation.");
                }

                if (input.Direction != PaymentDirection.Received)
                {
                    throw new ValidationException(
                        "The payment could not be recorded.",
                        FinanceServiceHelpers.FieldError("allocations",
                            "A receivable can only be settled by a payment received."));
                }

                if (!string.Equals(receivable.CurrencyCode, currency, StringComparison.Ordinal))
                {
                    throw new ValidationException(
                        "The payment could not be recorded.",
                        FinanceServiceHelpers.FieldError("allocations",
                            $"Receivable '{receivable.Reference}' is in {receivable.CurrencyCode}; the payment is in {currency}."));
                }

                // The document is only mutated after the journal has been posted (see
                // ApplyAllocationsAsync). Mutating here would let the posting engine's own
                // SaveChangesAsync flush it early, and the final save would then fail on a
                // stale xmin concurrency token.
                var appliedAmount = FinanceDomain.NormalizeAmount(allocation.Amount);
                allocatedTotal += appliedAmount;
                normalizedAllocations.Add((receivable, (AccountsPayableEntity?)null, appliedAmount));
                continue;
            }

            var payableId = allocation.PayableId!.Value;
            var payable = await _context.AccountsPayable
                .FirstOrDefaultAsync(a => a.Id == payableId, cancellationToken)
                ?? throw new ValidationException(
                    "The payment could not be recorded.",
                    FinanceServiceHelpers.FieldError("allocations", $"Payable '{payableId}' does not exist."));

            if (payable.Status == PayableStatus.Cancelled)
            {
                throw new ConflictException($"Payable '{payable.Reference}' is cancelled and cannot receive an allocation.");
            }

            if (input.Direction != PaymentDirection.Made)
            {
                throw new ValidationException(
                    "The payment could not be recorded.",
                    FinanceServiceHelpers.FieldError("allocations",
                        "A payable can only be settled by a payment made."));
            }

            if (!string.Equals(payable.CurrencyCode, currency, StringComparison.Ordinal))
            {
                throw new ValidationException(
                    "The payment could not be recorded.",
                    FinanceServiceHelpers.FieldError("allocations",
                        $"Payable '{payable.Reference}' is in {payable.CurrencyCode}; the payment is in {currency}."));
            }

            var payableAmount = FinanceDomain.NormalizeAmount(allocation.Amount);

            // The document is only mutated after the journal has been posted (see the
            // allocation loop). Mutating here would let the posting engine's own
            // SaveChangesAsync flush it early, and the final save would then fail on a
            // stale xmin concurrency token.
            allocatedTotal += payableAmount;
            normalizedAllocations.Add(((AccountsReceivableEntity?)null, payable, payableAmount));
        }

        if (allocatedTotal > amount)
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                FinanceServiceHelpers.FieldError("allocations",
                    $"The allocations total {allocatedTotal}, which exceeds the payment amount of {amount}."));
        }

        var ruleCode = FinanceServiceHelpers.NormalizeOptional(input.RuleCode)
                       ?? (input.Direction == PaymentDirection.Received
                           ? FinancialPostingRuleCodes.MemberAccountPayment
                           : FinancialPostingRuleCodes.SupplierPayment);

        // A single-document allocation is recorded on the journal so the GL entry and
        // the settled document are directly linked.
        var singleReceivableId = normalizedAllocations.Count == 1 ? normalizedAllocations[0].Receivable?.Id : null;
        var singlePayableId = normalizedAllocations.Count == 1 ? normalizedAllocations[0].Payable?.Id : null;

        var sourceType = FinanceServiceHelpers.NormalizeOptional(input.SourceType) ?? "FinancePayment";
        var paymentSourceId = input.SourceId ?? Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var journal = await _posting.PostAsync(
                new FinancialPostingRequest(
                    ruleCode,
                    sourceType,
                    paymentSourceId,
                    input.SourceReference,
                    input.TransactionDate,
                    input.TransactionDate,
                    currency,
                    null,
                    null,
                    FinanceServiceHelpers.NormalizeOptional(input.Description) ?? $"Payment {input.Direction}",
                    [new FinancialPostingAmount(null, amount)],
                    singleReceivableId,
                    singlePayableId),
                actorUserId,
                string.Empty,
                cancellationToken);

            var unapplied = FinanceDomain.NormalizeAmount(amount - allocatedTotal);

            var payment = new PaymentEntity
            {
                Id = paymentId,
                Reference = await _references.GenerateAsync(PaymentReference.Prefix, PaymentReference.Width, cancellationToken),
                Direction = input.Direction,
                MemberId = input.Direction == PaymentDirection.Received ? input.MemberId : null,
                SupplierId = input.Direction == PaymentDirection.Made ? input.SupplierId : null,
                MemberAccountId = input.MemberAccountId,
                AccountTransactionId = input.AccountTransactionId,
                SourceType = sourceType,
                SourceId = paymentSourceId,
                SourceReference = FinanceServiceHelpers.NormalizeOptional(input.SourceReference),
                Amount = amount,
                UnappliedAmount = unapplied,
                CurrencyCode = currency,
                TransactionDate = input.TransactionDate,
                ValueDate = input.ValueDate,
                PaymentMethodCode = FinanceServiceHelpers.NormalizeOptional(input.PaymentMethodCode),
                ExternalReference = FinanceServiceHelpers.NormalizeOptional(input.ExternalReference),
                JournalId = journal.Id,
                Description = FinanceServiceHelpers.NormalizeOptional(input.Description),
                IdempotencyKey = key,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = _clock.UtcNow,
                UpdatedAtUtc = _clock.UtcNow,
            };

            foreach (var (receivable, payable, allocationAmount) in normalizedAllocations)
            {
                // Safe to mutate the documents only now: the posting engine has already
                // saved its journal, so nothing will flush these ahead of the commit.
                if (receivable is not null)
                {
                    EntityFrameworkReceivableService.ApplyAllocation(
                        receivable,
                        allocationAmount,
                        actorUserId,
                        _clock.UtcNow);

                    var documentAllocation = new AccountsReceivableAllocationEntity
                    {
                        Id = Guid.CreateVersion7(),
                        ReceivableId = receivable.Id,
                        Receivable = receivable,
                        PaymentId = payment.Id,
                        Payment = payment,
                        Amount = allocationAmount,
                        AllocatedAtUtc = _clock.UtcNow,
                        AllocatedByUserId = actorUserId,
                    };
                    receivable.Allocations.Add(documentAllocation);

                    // Must be added explicitly. EF only cascades Added from an entity that
                    // was itself Added; an allocation reached solely through a tracked,
                    // Unchanged receivable is treated as Modified (its key is already set)
                    // and EF then issues an UPDATE against a row that does not exist.
                    _context.AccountsReceivableAllocations.Add(documentAllocation);
                }

                if (payable is not null)
                {
                    EntityFrameworkPayableService.ApplyAllocation(
                        payable,
                        allocationAmount,
                        actorUserId,
                        _clock.UtcNow);

                    var documentAllocation = new AccountsPayableAllocationEntity
                    {
                        Id = Guid.CreateVersion7(),
                        PayableId = payable.Id,
                        Payable = payable,
                        PaymentId = payment.Id,
                        Payment = payment,
                        Amount = allocationAmount,
                        AllocatedAtUtc = _clock.UtcNow,
                        AllocatedByUserId = actorUserId,
                    };
                    payable.Allocations.Add(documentAllocation);
                    _context.AccountsPayableAllocations.Add(documentAllocation);
                }

                payment.Allocations.Add(new FinancePaymentAllocationEntity
                {
                    Id = Guid.CreateVersion7(),
                    PaymentId = payment.Id,
                    ReceivableId = receivable?.Id,
                    PayableId = payable?.Id,
                    Amount = allocationAmount,
                    CreatedAtUtc = _clock.UtcNow,
                });
            }

            _context.Payments.Add(payment);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            await _unitOfWork.RollbackAsync(cancellationToken);

            // Lost a race on the idempotency key or the (SourceType, SourceId) unique index.
            if (key is not null)
            {
                var replay = await GetByIdempotencyKeyAsync(key, cancellationToken);
                if (replay is not null)
                {
                    return replay;
                }
            }

            if (input.SourceId is { } racedSourceId)
            {
                var raced = await _context.Payments
                    .FullPayments()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.SourceType == sourceType && p.SourceId == racedSourceId,
                        cancellationToken);

                if (raced is not null)
                {
                    return ToDto(raced);
                }
            }

            throw;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return await GetAsync(paymentId, cancellationToken);
    }

    internal static PaymentDto ToDto(PaymentEntity p) => new(
        p.Id,
        p.Reference,
        p.Direction,
        p.MemberId,
        p.Member?.MemberNumber,
        p.SupplierId,
        p.Supplier?.SupplierCode,
        p.MemberAccountId,
        p.AccountTransactionId,
        p.SourceType,
        p.SourceId,
        p.SourceReference,
        p.Amount,
        p.UnappliedAmount,
        p.CurrencyCode,
        p.TransactionDate,
        p.ValueDate,
        p.PaymentMethodCode,
        p.ExternalReference,
        p.JournalId,
        p.Journal?.Reference,
        p.Description,
        p.IdempotencyKey,
        p.CreatedByUserId,
        p.CreatedAtUtc,
        p.Allocations
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new PaymentAllocationDto(
                a.Id,
                a.PaymentId,
                a.ReceivableId,
                a.PayableId,
                a.Receivable?.Reference,
                a.Payable?.Reference,
                a.Amount,
                a.CreatedAtUtc))
            .ToList());
}
