using ClubStack.Application.Accounts;
using ClubStack.Application.Exceptions;
using ClubStack.Application.References;
using ClubStack.Domain.Accounts;
using ClubStack.Domain.Common;
using ClubStack.Infrastructure.Finance;
using ClubStack.Infrastructure.Membership;
using ClubStack.Infrastructure.Options;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubStack.Infrastructure.Accounts;

/// <inheritdoc />
public sealed class EntityFrameworkMemberAccountService : IMemberAccountService
{
    private const string AccountNumberPrefix = "ACC";
    private const string ChargeReferencePrefix = "CHG";
    private const string PaymentReferencePrefix = "PAY";
    private const string CreditReferencePrefix = "CRT";
    private const string AdjustmentReferencePrefix = "ADJ";
    private const string ReversalReferencePrefix = "REV";
    private const string StatementReferencePrefix = "STMT";

    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;
    private readonly AccountOptions _options;
    private readonly IFinanceRequirementWriter _financeRequirements;

    public EntityFrameworkMemberAccountService(
        ClubStackDbContext context,
        IReferenceGenerator referenceGenerator,
        ISystemClock clock,
        IOptions<AccountOptions> options,
        IFinanceRequirementWriter financeRequirements)
    {
        _context = context;
        _referenceGenerator = referenceGenerator;
        _clock = clock;
        _options = options.Value;
        _financeRequirements = financeRequirements;
    }

    public async Task<MemberAccountDto> GetAccountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Members
            .Include(m => m.Person)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new NotFoundException($"Member '{memberId}' was not found.");

        var account = await EnsureAccountAsync(member, cancellationToken);
        return await MemberAccountMappings.ToDtoAsync(this, account, _context, cancellationToken);
    }

    public async Task<MemberAccountDto> GetAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: false, cancellationToken);
        return await MemberAccountMappings.ToDtoAsync(this, account, _context, cancellationToken);
    }

    public async Task<AccountBalanceDto> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: false, cancellationToken);
        return await ComputeBalanceAsync(account, cancellationToken);
    }

    public async Task<AccountTransactionSearchResult> SearchTransactionsAsync(AccountTransactionSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePage(input.Page, input.PageSize);
        await LoadAccountAsync(input.AccountId, tracked: false, cancellationToken);

        IQueryable<AccountTransactionEntity> query = _context.AccountTransactions
            .Include(t => t.PaymentMethod)
            .Where(t => t.AccountId == input.AccountId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.TransactionDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AccountTransactionSearchResult(
            items.Select(AccountMappings.ToTransactionDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<AccountTransactionDto> ChargeAsync(Guid accountId, ChargeInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: true, cancellationToken);
        EnsureAccountAcceptsCharges(account);

        ValidateAmount(input.Amount, "amount");
        EnsureCurrency(account, input.CurrencyCode);
        EnsureDescription(input.Description);

        var now = _clock.UtcNow;
        var transaction = new AccountTransactionEntity
        {
            AccountId = account.Id,
            TransactionType = AccountTransactionType.Charge,
            Status = AccountTransactionStatus.Posted,
            CurrencyCode = account.CurrencyCode,
            Amount = input.Amount,
            Description = input.Description.Trim(),
            Reference = string.IsNullOrWhiteSpace(input.Reference)
                ? await _referenceGenerator.GenerateAsync(ChargeReferencePrefix, cancellationToken: cancellationToken)
                : input.Reference.Trim(),
            SourceType = NormalizeOptional(input.SourceType),
            SourceId = input.SourceId,
            TransactionDateUtc = now,
            BusinessDateUtc = input.BusinessDateUtc ?? now,
            CreatedAtUtc = now,
        };

        _context.AccountTransactions.Add(transaction);
        if (_context.Database.CurrentTransaction is null)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return AccountMappings.ToTransactionDto(transaction);
    }

    public async Task<AccountTransactionDto> RecordPaymentAsync(Guid accountId, PaymentInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: true, cancellationToken);
        EnsureAccountAcceptsCharges(account);

        ValidateAmount(input.Amount, "amount");
        EnsureCurrency(account, input.CurrencyCode);
        EnsureDescription(input.Description);

        var paymentMethod = await _context.PaymentMethods
            .FirstOrDefaultAsync(p => p.Code == input.PaymentMethodCode.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException($"Payment method '{input.PaymentMethodCode}' was not found.");

        if (!string.IsNullOrWhiteSpace(input.ExternalReference))
        {
            var duplicate = await _context.AccountTransactions.AnyAsync(
                t => t.AccountId == account.Id
                     && t.CurrencyCode == account.CurrencyCode
                     && t.TransactionType == AccountTransactionType.Payment
                     && t.Status == AccountTransactionStatus.Posted
                     && t.ExternalReference != null
                     && t.ExternalReference == input.ExternalReference,
                cancellationToken);
            if (duplicate)
            {
                throw new ConflictException("A payment with this external reference already exists on the account.");
            }
        }

        var allocations = await ResolveAllocationsAsync(account, input.Allocations, input.Amount, cancellationToken);

        var now = _clock.UtcNow;
        var payment = new AccountTransactionEntity
        {
            AccountId = account.Id,
            TransactionType = AccountTransactionType.Payment,
            Status = AccountTransactionStatus.Posted,
            PaymentState = AccountPaymentState.Recorded,
            CurrencyCode = account.CurrencyCode,
            Amount = input.Amount,
            Description = input.Description.Trim(),
            Reference = await _referenceGenerator.GenerateAsync(PaymentReferencePrefix, cancellationToken: cancellationToken),
            ExternalReference = NormalizeOptional(input.ExternalReference),
            PaymentMethodId = paymentMethod.Id,
            PaymentMethod = paymentMethod,
            TransactionDateUtc = now,
            BusinessDateUtc = input.BusinessDateUtc ?? now,
            CreatedAtUtc = now,
        };

        _context.AccountTransactions.Add(payment);
        foreach (var allocation in allocations)
        {
            _context.PaymentAllocations.Add(new PaymentAllocationEntity
            {
                AccountId = account.Id,
                FromTransactionId = payment.Id,
                ToChargeTransactionId = allocation.ChargeId,
                AllocatedAmount = allocation.Amount,
                CreatedAtUtc = now,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Phase 8 §14.3: the payment owes Dr cash/bank / Cr member receivable. The D3
        // transaction stays the authoritative record; the outbox message is written in the
        // same unit of work so the two commit together.
        await _financeRequirements.RequireMemberPaymentPostingAsync(
            payment.Id,
            account.Id,
            account.MemberId,
            payment.Reference,
            payment.Description,
            payment.Amount,
            payment.CurrencyCode,
            payment.TransactionDateUtc,
            payment.BusinessDateUtc,
            string.Empty,
            cancellationToken);

        return AccountMappings.ToTransactionDto(payment);
    }

    public async Task<AccountTransactionDto> CreateCreditAsync(Guid accountId, CreditInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: true, cancellationToken);
        EnsureAccountAcceptsCharges(account);

        ValidateAmount(input.Amount, "amount");
        EnsureCurrency(account, input.CurrencyCode);
        EnsureDescription(input.Description);

        var allocations = await ResolveAllocationsAsync(account, input.Allocations, input.Amount, cancellationToken);

        var now = _clock.UtcNow;
        var credit = new AccountTransactionEntity
        {
            AccountId = account.Id,
            TransactionType = AccountTransactionType.Credit,
            Status = AccountTransactionStatus.Posted,
            CurrencyCode = account.CurrencyCode,
            Amount = input.Amount,
            Description = input.Description.Trim(),
            Reference = string.IsNullOrWhiteSpace(input.Reference)
                ? await _referenceGenerator.GenerateAsync(CreditReferencePrefix, cancellationToken: cancellationToken)
                : input.Reference.Trim(),
            ExternalReference = NormalizeOptional(input.ExternalReference),
            TransactionDateUtc = now,
            BusinessDateUtc = input.BusinessDateUtc ?? now,
            CreatedAtUtc = now,
        };

        _context.AccountTransactions.Add(credit);
        foreach (var allocation in allocations)
        {
            _context.PaymentAllocations.Add(new PaymentAllocationEntity
            {
                AccountId = account.Id,
                FromTransactionId = credit.Id,
                ToChargeTransactionId = allocation.ChargeId,
                AllocatedAmount = allocation.Amount,
                CreatedAtUtc = now,
            });
        }

        if (_context.Database.CurrentTransaction is null)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return AccountMappings.ToTransactionDto(credit);
    }

    public async Task<AccountTransactionDto> AdjustAsync(Guid accountId, AdjustmentInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: true, cancellationToken);
        if (input.Amount == 0m)
        {
            throw new ValidationException(
                "The adjustment could not be recorded.",
                MembershipServiceHelpers.FieldError("amount", "An adjustment amount must not be zero."));
        }

        EnsureDescription(input.Description);
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The adjustment could not be recorded.",
                MembershipServiceHelpers.FieldError("reason", "A reason is required."));
        }

        var now = _clock.UtcNow;
        var adjustment = new AccountTransactionEntity
        {
            AccountId = account.Id,
            TransactionType = AccountTransactionType.Adjustment,
            Status = AccountTransactionStatus.Posted,
            CurrencyCode = account.CurrencyCode,
            Amount = input.Amount,
            Description = input.Description.Trim(),
            Reason = input.Reason.Trim(),
            Reference = await _referenceGenerator.GenerateAsync(AdjustmentReferencePrefix, cancellationToken: cancellationToken),
            TransactionDateUtc = now,
            BusinessDateUtc = input.BusinessDateUtc ?? now,
            CreatedAtUtc = now,
        };

        _context.AccountTransactions.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);
        return AccountMappings.ToTransactionDto(adjustment);
    }

    public async Task<AccountTransactionDto> ReverseAsync(Guid accountId, Guid transactionId, ReversalInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: true, cancellationToken);
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The reversal could not be recorded.",
                MembershipServiceHelpers.FieldError("reason", "A reason is required."));
        }

        var target = await _context.AccountTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.AccountId == account.Id, cancellationToken)
            ?? throw new NotFoundException($"Transaction '{transactionId}' was not found on this account.");

        if (target.Status != AccountTransactionStatus.Posted)
        {
            throw new ConflictException("The transaction has already been reversed.");
        }

        if (target.TransactionType != AccountTransactionType.Payment && target.TransactionType != AccountTransactionType.Credit)
        {
            throw new ConflictException("Only payments and credits can be reversed.");
        }

        var now = _clock.UtcNow;
        target.Status = AccountTransactionStatus.Reversed;
        target.UpdatedAtUtc = now;

        var reversal = new AccountTransactionEntity
        {
            AccountId = account.Id,
            TransactionType = AccountTransactionType.Reversal,
            Status = AccountTransactionStatus.Posted,
            CurrencyCode = account.CurrencyCode,
            Amount = target.Amount,
            Description = $"Reversal of {target.TransactionType.ToString().ToLowerInvariant()} '{target.Reference}'.",
            Reason = input.Reason.Trim(),
            Reference = await _referenceGenerator.GenerateAsync(ReversalReferencePrefix, cancellationToken: cancellationToken),
            ReverseOfTransactionId = target.Id,
            TransactionDateUtc = now,
            BusinessDateUtc = now,
            CreatedAtUtc = now,
        };

        _context.AccountTransactions.Add(reversal);
        await _context.SaveChangesAsync(cancellationToken);
        return AccountMappings.ToTransactionDto(reversal);
    }

    public async Task<StatementDto> GenerateStatementAsync(Guid accountId, StatementPeriodInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: false, cancellationToken);
        var from = input.FromDateUtc ?? account.OpenedAtUtc;
        var to = input.ToDateUtc ?? _clock.UtcNow;

        if (from > to)
        {
            throw new ValidationException(
                "The statement could not be generated.",
                MembershipServiceHelpers.FieldError("toDateUtc", "The statement end date must not be before its start date."));
        }

        var transactions = await GetTransactionsAsync(account.Id, cancellationToken);
        var opening = transactions
            .Where(t => t.Status == AccountTransactionStatus.Posted && t.BusinessDateUtc < from)
            .Sum(t => AccountLedger.SignedAmount(t.TransactionType, t.Amount));
        var closing = transactions
            .Where(t => t.Status == AccountTransactionStatus.Posted && t.BusinessDateUtc <= to)
            .Sum(t => AccountLedger.SignedAmount(t.TransactionType, t.Amount));

        var now = _clock.UtcNow;
        var statement = new AccountStatementEntity
        {
            AccountId = account.Id,
            StatementNumber = await _referenceGenerator.GenerateAsync(StatementReferencePrefix, cancellationToken: cancellationToken),
            FromDateUtc = from,
            ToDateUtc = to,
            OpeningBalance = opening,
            ClosingBalance = closing,
            GeneratedByUserId = actorUserId,
            GeneratedAtUtc = now,
        };

        _context.AccountStatements.Add(statement);
        await _context.SaveChangesAsync(cancellationToken);

        var inPeriod = transactions
            .Where(t => t.BusinessDateUtc >= from && t.BusinessDateUtc <= to)
            .OrderBy(t => t.BusinessDateUtc)
            .Select(AccountMappings.ToTransactionDto)
            .ToList();

        return new StatementDto(
            account.Id,
            account.AccountNumber,
            account.Member.MemberNumber,
            MemberDisplayName(account.Member),
            account.CurrencyCode,
            statement.StatementNumber,
            from,
            to,
            opening,
            closing,
            ComputeBalance(transactions),
            statement.GeneratedAtUtc,
            statement.GeneratedByUserId,
            inPeriod);
    }

    public async Task<AgingDto> GetAgingAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await LoadAccountAsync(accountId, tracked: false, cancellationToken);
        var transactions = await GetTransactionsAsync(account.Id, cancellationToken);
        var outstanding = await ComputeOutstandingChargesCoreAsync(account.Id, transactions, cancellationToken);
        var now = _clock.UtcNow;

        var buckets = AccountAging.ComputeBrackets(
            now,
            _options.AgingBucketDays,
            _options.AgingBucketCount,
            outstanding.Select(o => (o.BusinessDateUtc, o.Outstanding)));

        var items = outstanding
            .Where(o => o.Outstanding > 0m)
            .OrderBy(o => o.BusinessDateUtc)
            .Select(o => new AgingItemDto(
                o.TransactionId,
                o.Description,
                o.BusinessDateUtc,
                Math.Max(0, (now.UtcDateTime.Date - o.BusinessDateUtc.UtcDateTime.Date).Days),
                o.Outstanding))
            .ToList();

        // TotalOutstanding is the account's net derived balance (posted debits minus
        // posted credits), so the aging report agrees with the balance endpoint even
        // when unallocated payments/credits have not been applied to a specific charge.
        return new AgingDto(
            now,
            account.CurrencyCode,
            _options.AgingBucketDays,
            ComputeBalance(transactions),
            items.Count,
            buckets.Select(b => new AgingBucketDto(b.Label, b.DaysFrom, b.DaysTo, b.OutstandingAmount)).ToList(),
            items);
    }

    internal async Task<AccountBalanceDto> ComputeBalanceAsync(MemberAccountEntity account, CancellationToken cancellationToken)
    {
        var transactions = await GetTransactionsAsync(account.Id, cancellationToken);
        var allocations = await _context.PaymentAllocations
            .Include(a => a.FromTransaction)
            .AsNoTracking()
            .Where(a => a.AccountId == account.Id)
            .ToListAsync(cancellationToken);

        var posted = transactions.Where(t => t.Status == AccountTransactionStatus.Posted).ToList();
        var amountDue = ComputeBalance(transactions);
        var creditsBalance = posted
            .Where(t => t.TransactionType == AccountTransactionType.Credit)
            .Sum(t => t.Amount);

        var paidAmount = posted
            .Where(t => t.TransactionType == AccountTransactionType.Payment)
            .Sum(t => t.Amount);
        var allocatedToPayments = allocations
            .Where(a => a.FromTransaction.Status == AccountTransactionStatus.Posted
                        && a.FromTransaction.TransactionType == AccountTransactionType.Payment)
            .Sum(a => a.AllocatedAmount);

        var unallocatedPayments = Math.Max(0m, paidAmount - allocatedToPayments);
        var overdue = await ComputeOverdueAsync(account, transactions, cancellationToken);

        return new AccountBalanceDto(account.CurrencyCode, amountDue, creditsBalance, unallocatedPayments, overdue, _clock.UtcNow);
    }

    private async Task<decimal> ComputeOverdueAsync(
        MemberAccountEntity account,
        IReadOnlyList<AccountTransactionEntity> transactions,
        CancellationToken cancellationToken)
    {
        var outstanding = await ComputeOutstandingChargesCoreAsync(account.Id, transactions, cancellationToken);
        var now = _clock.UtcNow.Date;
        return outstanding
            .Where(o => o.Outstanding > 0m && (now - o.BusinessDateUtc.UtcDateTime.Date).Days >= _options.AgingBucketDays)
            .Sum(o => o.Outstanding);
    }

    private async Task<List<(Guid TransactionId, string Description, DateTimeOffset BusinessDateUtc, decimal Outstanding)>> ComputeOutstandingChargesCoreAsync(
        Guid accountId,
        IReadOnlyList<AccountTransactionEntity> transactions,
        CancellationToken cancellationToken)
    {
        var allocations = await _context.PaymentAllocations
            .Include(a => a.FromTransaction)
            .AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var settledByCharge = allocations
            .Where(a => a.FromTransaction.Status == AccountTransactionStatus.Posted)
            .GroupBy(a => a.ToChargeTransactionId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.AllocatedAmount));

        return transactions
            .Where(t => t.TransactionType == AccountTransactionType.Charge && t.Status == AccountTransactionStatus.Posted)
            .Select(t => (
                t.Id,
                t.Description,
                t.BusinessDateUtc,
                settledByCharge.TryGetValue(t.Id, out var settled) ? decimal.Round(t.Amount - settled, 4) : t.Amount))
            .ToList();
    }

    private async Task<List<(Guid ChargeId, decimal Amount)>> ResolveAllocationsAsync(
        MemberAccountEntity account,
        IReadOnlyList<PaymentAllocationInput>? inputAllocations,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        if (inputAllocations is null || inputAllocations.Count == 0)
        {
            return new List<(Guid, decimal)>();
        }

        var allocations = new List<(Guid ChargeId, decimal Amount)>();
        var transactions = await GetTransactionsAsync(account.Id, cancellationToken);
        var postedCharges = transactions
            .Where(t => t.TransactionType == AccountTransactionType.Charge && t.Status == AccountTransactionStatus.Posted)
            .ToDictionary(t => t.Id);

        var settled = await ComputeSettledByChargeAsync(account.Id, cancellationToken);
        foreach (var allocation in inputAllocations)
        {
            if (allocation.Amount <= 0m)
            {
                throw new ValidationException(
                    "The payment could not be recorded.",
                    MembershipServiceHelpers.FieldError("allocations", "Each allocation amount must be positive."));
            }

            if (!postedCharges.TryGetValue(allocation.ChargeTransactionId, out var charge))
            {
                throw new ValidationException(
                    "The payment could not be recorded.",
                    MembershipServiceHelpers.FieldError("allocations", "The allocation references a charge that is not open on this account."));
            }

            var outstanding = decimal.Round(charge.Amount - (settled.TryGetValue(charge.Id, out var already) ? already : 0m), 4);
            if (allocation.Amount > outstanding)
            {
                throw new ConflictException($"The allocation cannot exceed the outstanding amount of charge '{charge.Reference}'.");
            }

            allocations.Add((allocation.ChargeTransactionId, allocation.Amount));
        }

        if (allocations.Sum(a => a.Amount) > totalAmount)
        {
            throw new ValidationException(
                "The payment could not be recorded.",
                MembershipServiceHelpers.FieldError("allocations", "The total allocation cannot exceed the payment amount."));
        }

        return allocations;
    }

    private async Task<Dictionary<Guid, decimal>> ComputeSettledByChargeAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await _context.PaymentAllocations
            .Include(a => a.FromTransaction)
            .AsNoTracking()
            .Where(a => a.AccountId == accountId
                        && a.FromTransaction.Status == AccountTransactionStatus.Posted)
            .GroupBy(a => a.ToChargeTransactionId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(a => a.AllocatedAmount), cancellationToken);
    }

    private async Task<List<AccountTransactionEntity>> GetTransactionsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await _context.AccountTransactions
            .Where(t => t.AccountId == accountId)
            .ToListAsync(cancellationToken);
    }

    private async Task<MemberAccountEntity> EnsureAccountAsync(MemberEntity member, CancellationToken cancellationToken)
    {
        var existing = await _context.MemberAccounts
            .Include(a => a.Member)
                .ThenInclude(m => m!.Person)
            .FirstOrDefaultAsync(a => a.MemberId == member.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var currencyCode = await _context.Currencies
            .Where(c => c.IsDefault)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No default currency is configured for the club.");

        var now = _clock.UtcNow;
        var account = new MemberAccountEntity
        {
            MemberId = member.Id,
            AccountNumber = await _referenceGenerator.GenerateAsync(AccountNumberPrefix, cancellationToken: cancellationToken),
            Status = MemberAccountStatus.Active,
            CurrencyCode = currencyCode,
            OpenedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.MemberAccounts.Add(account);
        if (_context.Database.CurrentTransaction is null)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await _context.MemberAccounts
            .Include(a => a.Member)
                .ThenInclude(m => m!.Person)
            .AsNoTracking()
            .FirstAsync(a => a.Id == account.Id, cancellationToken);
    }

    private async Task<MemberAccountEntity> LoadAccountAsync(Guid accountId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<MemberAccountEntity> query = _context.MemberAccounts
            .Include(a => a.Member)
                .ThenInclude(m => m!.Person);
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
               ?? throw new NotFoundException($"Member account '{accountId}' was not found.");
    }

    private static void EnsureAccountAcceptsCharges(MemberAccountEntity account)
    {
        if (account.Status != MemberAccountStatus.Active)
        {
            throw new ConflictException($"The member cannot be charged while the account is {account.Status.ToString().ToLowerInvariant()}.");
        }
    }

    private static void ValidateAmount(decimal amount, string fieldName)
    {
        if (amount <= 0m)
        {
            throw new ValidationException(
                "The amount is invalid.",
                MembershipServiceHelpers.FieldError(fieldName, "The amount must be positive."));
        }
    }

    private void EnsureCurrency(MemberAccountEntity account, string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return;
        }

        if (!string.Equals(currencyCode.Trim().ToUpperInvariant(), account.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ValidationException(
                "The transaction could not be recorded.",
                MembershipServiceHelpers.FieldError("currencyCode", $"The transaction currency must match the account currency ('{account.CurrencyCode}')."));
        }
    }

    private static void EnsureDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ValidationException(
                "The transaction could not be recorded.",
                MembershipServiceHelpers.FieldError("description", "A description is required."));
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal ComputeBalance(IEnumerable<AccountTransactionEntity> transactions)
        => transactions.Sum(t => AccountLedger.SignedAmount(t.TransactionType, t.Amount));

    private static string MemberDisplayName(MemberEntity member)
        => member.Person is { } person && person.PreferredName.Length > 0 ? person.PreferredName : member.Person?.LegalName ?? string.Empty;

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 250);
        return (safePage, safePageSize);
    }
}

internal static class AccountMappings
{
    public static AccountTransactionDto ToTransactionDto(AccountTransactionEntity t) => new(
        t.Id,
        t.AccountId,
        t.TransactionType.ToString(),
        t.Status.ToString(),
        t.CurrencyCode,
        t.Amount,
        AccountLedger.SignedAmount(t.TransactionType, t.Amount),
        AccountLedger.IsDebitSide(t.TransactionType, t.Amount),
        t.Description,
        t.Reference,
        t.ExternalReference,
        t.Reason,
        t.SourceType,
        t.SourceId,
        t.ReverseOfTransactionId,
        t.PaymentMethodId,
        t.PaymentMethod?.Code,
        t.PaymentState?.ToString(),
        t.TransactionDateUtc,
        t.BusinessDateUtc,
        t.CreatedAtUtc);
}

internal static class MemberAccountMappings
{
    public static async Task<MemberAccountDto> ToDtoAsync(
        EntityFrameworkMemberAccountService service,
        MemberAccountEntity account,
        ClubStackDbContext context,
        CancellationToken cancellationToken)
    {
        var balance = await service.ComputeBalanceAsync(account, cancellationToken);
        return new MemberAccountDto(
            account.Id,
            account.MemberId,
            account.Member.MemberNumber,
            account.Member.Person is { } person && person.PreferredName.Length > 0 ? person.PreferredName : account.Member.Person?.LegalName ?? string.Empty,
            account.AccountNumber,
            account.Status.ToString(),
            account.CurrencyCode,
            account.OpenedAtUtc,
            account.ClosedAtUtc,
            balance,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);
    }
}