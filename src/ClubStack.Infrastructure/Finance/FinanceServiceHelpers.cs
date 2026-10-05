using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Finance;

/// <summary>Shared D6 finance plumbing: base currency, account lookup, paging and parsing.</summary>
internal static class FinanceServiceHelpers
{
    /// <summary>
    /// B17: the club-level configurable base currency. Stored in the existing
    /// <c>system_configurations</c> table so no new configuration store is introduced.
    /// </summary>
    public const string BaseCurrencyConfigKey = "Finance.BaseCurrency";

    public const string DefaultBaseCurrency = "ZAR";

    public static async Task<string> GetBaseCurrencyAsync(ClubStackDbContext context, CancellationToken cancellationToken)
    {
        var configured = await context.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.Key == BaseCurrencyConfigKey)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(configured)
            ? DefaultBaseCurrency
            : FinanceDomain.NormalizeCurrency(configured);
    }

    public static async Task SetBaseCurrencyAsync(
        ClubStackDbContext context,
        ISystemClock clock,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        var code = FinanceDomain.NormalizeCurrency(currencyCode);

        var row = await context.SystemConfigurations
            .FirstOrDefaultAsync(c => c.Key == BaseCurrencyConfigKey, cancellationToken);

        if (row is null)
        {
            context.SystemConfigurations.Add(new SystemConfigurationEntity
            {
                Key = BaseCurrencyConfigKey,
                Value = code,
                ValueType = "String",
                Description = "D6: club-level configurable base currency for financial posting (PM Decision Pack B17).",
                CreatedAtUtc = clock.UtcNow,
                UpdatedAtUtc = clock.UtcNow,
            });
        }
        else
        {
            row.Value = code;
            row.UpdatedAtUtc = clock.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Loads a posting-eligible account or throws a validation error naming the account.</summary>
    public static async Task<FinancialAccountEntity> RequirePostableAccountAsync(
        ClubStackDbContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await context.FinancialAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new ValidationException(
                "The journal could not be saved.",
                FieldError("lines", $"Financial account '{accountId}' does not exist."));

        if (account.Status != FinancialAccountStatus.Active)
        {
            throw new ValidationException(
                "The journal could not be saved.",
                FieldError("lines", $"Financial account '{account.Code}' ({account.Name}) is inactive and cannot be posted to."));
        }

        if (!account.IsPostingAllowed)
        {
            throw new ValidationException(
                "The journal could not be saved.",
                FieldError("lines", $"Financial account '{account.Code}' ({account.Name}) is a control account that does not accept postings."));
        }

        return account;
    }

    public static async Task RequireAccountPostableAsync(
        ClubStackDbContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new ValidationException(
                "The journal could not be posted.",
                FieldError("accountId", $"Financial account '{accountId}' does not exist."));

        if (account.Status != FinancialAccountStatus.Active || !account.IsPostingAllowed)
        {
            throw new ValidationException(
                "The journal could not be posted.",
                FieldError("accountId",
                    $"Financial account '{account.Code}' ({account.Name}) is inactive or not postable."));
        }
    }

    public static IDictionary<string, string[]> FieldError(string field, string message) =>
        new Dictionary<string, string[]> { [field] = [message] };

    public static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);
        return (normalizedPage, normalizedSize);
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Verifies that every currency an entity references exists in the currency
    /// reference table, so an unconfigured currency is rejected at the boundary
    /// rather than silently posted.
    /// </summary>
    public static async Task RequireKnownCurrencyAsync(
        ClubStackDbContext context,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        var code = FinanceDomain.NormalizeCurrency(currencyCode);
        var known = await context.Currencies
            .AsNoTracking()
            .AnyAsync(c => c.Code == code && c.IsActive, cancellationToken);

        if (!known)
        {
            throw new ValidationException(
                "The finance transaction could not be saved.",
                FieldError("currencyCode", $"Currency '{code}' is not an active configured currency."));
        }
    }

    public static TEnum ParseEnum<TEnum>(string? value, string field) where TEnum : struct, System.Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("The request could not be processed.", FieldError(field, "A value is required."));
        }

        if (!System.Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) ||
            !System.Enum.IsDefined(parsed))
        {
            throw new ValidationException(
                "The request could not be processed.",
                FieldError(field, $"'{value}' is not a valid {typeof(TEnum).Name} value."));
        }

        return parsed;
    }

    public static FinancialAccountDto ToAccountDto(FinancialAccountEntity a) => new(
        a.Id,
        a.Reference,
        a.Code,
        a.Name,
        a.AccountType,
        a.NormalBalance,
        a.Status,
        a.ParentAccountId,
        a.Description,
        a.IsSystem,
        a.CashKind,
        a.IsPostingAllowed,
        a.CurrencyCode,
        a.CreatedAtUtc,
        a.UpdatedAtUtc);

    public static JournalLineDto ToLineDto(JournalLineEntity l) => new(
        l.Id,
        l.LineNumber,
        l.FinancialAccountId,
        l.FinancialAccount.Code,
        l.FinancialAccount.Name,
        l.Direction,
        l.Amount,
        l.CurrencyCode,
        l.BaseAmount,
        l.Description,
        l.SourceLineId);

    public static JournalDto ToJournalDto(JournalEntity j)
    {
        var lines = j.Lines
            .OrderBy(l => l.LineNumber)
            .Select(ToLineDto)
            .ToList();

        var totalDebit = FinanceDomain.NormalizeAmount(lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.Amount));
        var totalCredit = FinanceDomain.NormalizeAmount(lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.Amount));

        return new JournalDto(
            j.Id,
            j.Reference,
            j.Status,
            j.Description,
            j.FinancialPeriodId,
            j.FinancialPeriod.Reference,
            j.TransactionDate,
            j.BusinessDate,
            j.TransactionCurrencyCode,
            j.BaseCurrencyCode,
            j.ExchangeRate,
            j.RateDateUtc,
            j.SourceType,
            j.SourceId,
            j.SourceReference,
            j.ReversalOfJournalId,
            j.ReversalOfJournal?.Reference,
            null,
            j.ReceivableId,
            j.PayableId,
            totalDebit,
            totalCredit,
            j.PostedAtUtc,
            j.PostedByUserId,
            j.CancelledReason,
            j.CreatedByUserId,
            j.CorrelationId,
            j.CreatedAtUtc,
            lines);
    }
}

/// <summary>Shared EF include paths so the mapping stays in one place.</summary>
internal static class FinanceQueries
{
    public static IQueryable<JournalEntity> FullJournals(this IQueryable<JournalEntity> query) =>
        query
            .Include(j => j.FinancialPeriod)
            .Include(j => j.ReversalOfJournal)
            .Include(j => j.Lines).ThenInclude(l => l.FinancialAccount);

    public static IQueryable<AccountsReceivableEntity> FullReceivables(this IQueryable<AccountsReceivableEntity> query) =>
        query
            .Include(a => a.Member).ThenInclude(m => m.Person)
            .Include(a => a.Journal)
            .Include(a => a.Allocations).ThenInclude(x => x.Payment);

    public static IQueryable<AccountsPayableEntity> FullPayables(this IQueryable<AccountsPayableEntity> query) =>
        query
            .Include(a => a.Supplier)
            .Include(a => a.Journal)
            .Include(a => a.Allocations).ThenInclude(x => x.Payment);

    public static IQueryable<PaymentEntity> FullPayments(this IQueryable<PaymentEntity> query) =>
        query
            .Include(p => p.Member)
            .Include(p => p.Supplier)
            .Include(p => p.Journal)
            .Include(p => p.Allocations).ThenInclude(a => a.Receivable)
            .Include(p => p.Allocations).ThenInclude(a => a.Payable);
}
