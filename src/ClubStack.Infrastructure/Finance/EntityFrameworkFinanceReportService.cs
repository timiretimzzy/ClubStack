using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Finance;

/// <summary>
/// Authoritative balance arithmetic over posted journal lines. Every D6 report and
/// the reconciliation variance are derived from these totals, so no report keeps
/// duplicated financial state.
/// </summary>
internal static class FinanceBalanceCalculator
{
    /// <summary>
    /// Net balance of one account in its own normal-balance terms, so a cash account
    /// (debit normal) and a loan account (credit normal) both read positive when
    /// healthy. Only Posted journals contribute.
    /// </summary>
    public static decimal NetBalance(IReadOnlyCollection<(JournalDirection Direction, decimal BaseAmount)> lines, NormalBalance normalBalance)
    {
        var debit = lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.BaseAmount);
        var credit = lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.BaseAmount);

        var net = normalBalance == NormalBalance.Debit ? debit - credit : credit - debit;
        return FinanceDomain.NormalizeAmount(net);
    }

    public static async Task<List<(JournalDirection Direction, decimal BaseAmount)>> LoadLinesAsync(
        ClubStackDbContext context,
        IReadOnlyCollection<Guid> accountIds,
        DateTimeOffset? upTo,
        CancellationToken cancellationToken)
    {
        var query = context.JournalLines
            .AsNoTracking()
            .Where(l => l.Journal.Status == JournalStatus.Posted);

        if (accountIds.Count > 0)
        {
            query = query.Where(l => accountIds.Contains(l.FinancialAccountId));
        }

        if (upTo is { } asOf)
        {
            query = query.Where(l => l.Journal.TransactionDate < asOf);
        }

        var lines = await query
            .Select(l => new { l.Direction, l.BaseAmount })
            .ToListAsync(cancellationToken);
        return lines.Select(x => (x.Direction, x.BaseAmount)).ToList();
    }
}

/// <summary>
/// The six mandated finance reports plus the finance overview (B16). Every report
/// reads the authoritative financial records; none of them materialise a parallel
/// set of numbers.
/// </summary>
public sealed class EntityFrameworkFinanceReportService : IFinanceReportService
{
    private readonly ClubStackDbContext _context;
    private readonly IFinancialPeriodService _periods;
    private readonly ISystemClock _clock;

    public EntityFrameworkFinanceReportService(
        ClubStackDbContext context,
        IFinancialPeriodService periods,
        ISystemClock clock)
    {
        _context = context;
        _periods = periods;
        _clock = clock;
    }

    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);

        var query = from line in _context.JournalLines.AsNoTracking()
                    join journal in _context.Journals.AsNoTracking() on line.JournalId equals journal.Id
                    where journal.Status == JournalStatus.Posted
                    group line by new { line.FinancialAccountId, line.Direction } into g
                    select new { g.Key.FinancialAccountId, g.Key.Direction, Total = g.Sum(x => x.BaseAmount) };

        if (asOf is { } cutoff)
        {
            query = from line in _context.JournalLines.AsNoTracking()
                    join journal in _context.Journals.AsNoTracking() on line.JournalId equals journal.Id
                    where journal.Status == JournalStatus.Posted && journal.TransactionDate < cutoff
                    group line by new { line.FinancialAccountId, line.Direction } into g
                    select new { g.Key.FinancialAccountId, g.Key.Direction, Total = g.Sum(x => x.BaseAmount) };
        }

        var totals = (await query.ToListAsync(cancellationToken))
            .GroupBy(t => t.FinancialAccountId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Debit: FinanceDomain.NormalizeAmount(g.Where(t => t.Direction == JournalDirection.Debit).Sum(t => t.Total)),
                    Credit: FinanceDomain.NormalizeAmount(g.Where(t => t.Direction == JournalDirection.Credit).Sum(t => t.Total))));

        // The totals are grouped client-side, so the participating account keys have to be
        // materialised too: a Dictionary.ContainsKey cannot be translated to SQL.
        var accountIds = totals.Keys.ToList();

        var accounts = await _context.FinancialAccounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var rows = accounts
            .Select(a =>
            {
                var t = totals[a.Id];
                var net = a.NormalBalance == NormalBalance.Debit ? t.Debit - t.Credit : t.Credit - t.Debit;
                return new TrialBalanceRowDto(
                    a.Code,
                    a.Name,
                    a.AccountType,
                    a.NormalBalance,
                    t.Debit,
                    t.Credit,
                    FinanceDomain.NormalizeAmount(net));
            })
            .ToList();

        var totalDebit = FinanceDomain.NormalizeAmount(rows.Sum(r => r.Debit));
        var totalCredit = FinanceDomain.NormalizeAmount(rows.Sum(r => r.Credit));

        return new TrialBalanceReportDto(
            baseCurrency,
            asOf,
            rows,
            totalDebit,
            totalCredit,
            FinanceDomain.NormalizeAmount(totalDebit - totalCredit));
    }

    public async Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(
        Guid financialAccountId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == financialAccountId, cancellationToken)
            ?? throw new NotFoundException($"Financial account '{financialAccountId}' was not found.");

        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);

        var query = _context.JournalLines
            .AsNoTracking()
            .Include(l => l.Journal).ThenInclude(j => j.FinancialPeriod)
            .Where(l => l.FinancialAccountId == financialAccountId && l.Journal.Status == JournalStatus.Posted);

        if (from is { } fromDate)
        {
            query = query.Where(l => l.Journal.TransactionDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(l => l.Journal.TransactionDate < toDate);
        }

        var lines = await query
            .OrderBy(l => l.Journal.TransactionDate)
            .ThenBy(l => l.Journal.Reference)
            .ThenBy(l => l.LineNumber)
            .ToListAsync(cancellationToken);

        // Opening balance is the account's posted balance before the requested window.
        var beforeFrom = from is null
            ? 0m
            : await ComputeBalanceAsync(account, from.Value, cancellationToken);

        var entries = new List<GeneralLedgerRowDto>(lines.Count);
        var running = beforeFrom;

        foreach (var line in lines)
        {
            running = account.NormalBalance == NormalBalance.Debit
                ? running + (line.Direction == JournalDirection.Debit ? line.BaseAmount : -line.BaseAmount)
                : running + (line.Direction == JournalDirection.Credit ? line.BaseAmount : -line.BaseAmount);

            entries.Add(new GeneralLedgerRowDto(
                line.Journal.TransactionDate,
                line.Journal.Reference,
                line.Journal.Description,
                account.Code,
                account.Name,
                line.Direction,
                line.BaseAmount,
                line.Description,
                line.Journal.SourceReference));
        }

        return new GeneralLedgerReportDto(
            account.Id,
            account.Code,
            account.Name,
            baseCurrency,
            from,
            to,
            FinanceDomain.NormalizeAmount(beforeFrom),
            FinanceDomain.NormalizeAmount(running),
            entries);
    }

    public Task<AgingReportDto> GetReceivableAgingAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default) =>
        BuildAgingAsync(isReceivable: true, asOf, cancellationToken);

    public Task<AgingReportDto> GetPayableAgingAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default) =>
        BuildAgingAsync(isReceivable: false, asOf, cancellationToken);

    private async Task<AgingReportDto> BuildAgingAsync(
        bool isReceivable,
        DateTimeOffset? asOf,
        CancellationToken cancellationToken)
    {
        var effectiveAsOf = asOf ?? _clock.UtcNow;
        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);

        List<AgingDocumentDto> documents;

        if (isReceivable)
        {
            var rows = await _context.AccountsReceivable
                .AsNoTracking()
                .Include(a => a.Member).ThenInclude(m => m.Person)
                .Where(a => a.Status != ReceivableStatus.Cancelled &&
                            a.OutstandingAmount > 0m &&
                            a.TransactionDate <= effectiveAsOf)
                .ToListAsync(cancellationToken);

            documents = rows.Select(a =>
            {
                var days = Math.Max(0, (int)(effectiveAsOf - a.TransactionDate).TotalDays);
                return new AgingDocumentDto(
                    a.Id,
                    a.Reference,
                    a.Member.MemberNumber,
                    a.Member.Person.LegalName,
                    a.TransactionDate,
                    a.DueDate,
                    days,
                    FinanceDomain.AgingBucketLabelFor(days),
                    a.OriginalAmount,
                    a.AllocatedAmount,
                    a.OutstandingAmount,
                    a.CurrencyCode);
            }).ToList();
        }
        else
        {
            var rows = await _context.AccountsPayable
                .AsNoTracking()
                .Include(a => a.Supplier)
                .Where(a => a.Status != PayableStatus.Cancelled &&
                            a.OutstandingAmount > 0m &&
                            a.TransactionDate <= effectiveAsOf)
                .ToListAsync(cancellationToken);

            documents = rows.Select(a =>
            {
                var days = Math.Max(0, (int)(effectiveAsOf - a.TransactionDate).TotalDays);
                return new AgingDocumentDto(
                    a.Id,
                    a.Reference,
                    a.Supplier.SupplierCode,
                    a.Supplier.Name,
                    a.TransactionDate,
                    a.DueDate,
                    days,
                    FinanceDomain.AgingBucketLabelFor(days),
                    a.OriginalAmount,
                    a.AllocatedAmount,
                    a.OutstandingAmount,
                    a.CurrencyCode);
            }).ToList();
        }

        // B16 fixes the buckets at 0-30, 31-60, 61-90 and 91+.
        var buckets = FinanceDomain.AgingBuckets
            .Select(b => new AgingBucketDto(
                b.Label,
                b.DaysFrom,
                b.DaysTo,
                FinanceDomain.NormalizeAmount(documents.Where(d => d.BucketLabel == b.Label).Sum(d => d.OutstandingAmount)),
                documents.Count(d => d.BucketLabel == b.Label)))
            .ToList();

        return new AgingReportDto(
            isReceivable,
            baseCurrency,
            effectiveAsOf,
            buckets,
            documents.OrderBy(d => d.DaysOutstanding).ThenBy(d => d.Reference).ToList(),
            FinanceDomain.NormalizeAmount(documents.Sum(d => d.OutstandingAmount)));
    }

    public async Task<CashPositionReportDto> GetCashPositionAsync(        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);
        var effectiveAsOf = asOf ?? _clock.UtcNow;

        // Cash Position is scoped to the accounts the chart of accounts classifies as
        // cash or bank; no account code is hard-coded here.
        var accounts = await _context.FinancialAccounts
            .AsNoTracking()
            .Where(a => a.CashKind != FinancialCashKind.None && a.Status == FinancialAccountStatus.Active)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var rows = new List<CashPositionRowDto>(accounts.Count);
        decimal totalCash = 0m;
        decimal totalBank = 0m;

        foreach (var account in accounts)
        {
            var lines = await LoadAccountLinesAsync(account.Id, effectiveAsOf, cancellationToken);
            var debit = FinanceDomain.NormalizeAmount(lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.BaseAmount));
            var credit = FinanceDomain.NormalizeAmount(lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.BaseAmount));
            var net = FinanceDomain.NormalizeAmount(debit - credit);

            rows.Add(new CashPositionRowDto(account.Code, account.Name, account.CashKind, debit, credit, net, baseCurrency));

            if (account.CashKind == FinancialCashKind.CashOnHand)
            {
                totalCash += net;
            }
            else
            {
                totalBank += net;
            }
        }

        return new CashPositionReportDto(
            baseCurrency,
            effectiveAsOf,
            rows,
            FinanceDomain.NormalizeAmount(totalCash),
            FinanceDomain.NormalizeAmount(totalBank));
    }

    public async Task<FinancialPeriodsReportDto> GetFinancialPeriodsAsync(CancellationToken cancellationToken = default)
    {
        var search = await _periods.SearchPeriodsAsync(
            new FinancialPeriodSearchInput(PageSize: 200),
            cancellationToken);

        var rows = search.Items
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .Select(p => new FinancialPeriodsReportRowDto(
                p.Reference,
                p.Year,
                p.Month,
                p.Name,
                p.Status,
                p.StartUtc,
                p.EndUtc,
                p.PostedDebitTotal,
                p.PostedCreditTotal,
                p.PostedJournalCount))
            .ToList();

        var current = await _context.FinancialPeriods
            .AsNoTracking()
            .Where(p => p.Status == FinancialPeriodStatus.Open)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync(cancellationToken);

        FinancialPeriodDto? currentDto = null;
        if (current is not null)
        {
            currentDto = await _periods.GetPeriodAsync(current.Id, cancellationToken);
        }

        return new FinancialPeriodsReportDto(rows, current?.Status, currentDto);
    }

    public async Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);

        var openReceivables = await _context.AccountsReceivable
            .AsNoTracking()
            .Where(a => a.Status != ReceivableStatus.Cancelled)
            .SumAsync(a => (decimal?)a.OutstandingAmount, cancellationToken) ?? 0m;

        var openPayables = await _context.AccountsPayable
            .AsNoTracking()
            .Where(a => a.Status != PayableStatus.Cancelled)
            .SumAsync(a => (decimal?)a.OutstandingAmount, cancellationToken) ?? 0m;

        var draftJournalCount = await _context.Journals
            .CountAsync(j => j.Status == JournalStatus.Draft, cancellationToken);

        var trialBalance = await GetTrialBalanceAsync(cancellationToken: cancellationToken);

        var cashAccounts = await _context.FinancialAccounts
            .AsNoTracking()
            .Where(a => a.CashKind != FinancialCashKind.None && a.Status == FinancialAccountStatus.Active)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        decimal cashAndBank = 0m;
        foreach (var accountId in cashAccounts)
        {
            var account = await _context.FinancialAccounts
                .AsNoTracking()
                .FirstAsync(a => a.Id == accountId, cancellationToken);
            var lines = await LoadAccountLinesAsync(account.Id, now, cancellationToken);
            var debit = lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.BaseAmount);
            var credit = lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.BaseAmount);
            cashAndBank += FinanceDomain.NormalizeAmount(debit - credit);
        }

        var unreconciledCashAccounts = await _context.FinancialReconciliations
            .AsNoTracking()
            .Where(r => cashAccounts.Contains(r.FinancialAccountId) && r.Status == FinancialReconciliationStatus.Completed)
            .Select(r => r.FinancialAccountId)
            .Distinct()
            .CountAsync(cancellationToken);

        var currentPeriodRow = await _context.FinancialPeriods
            .AsNoTracking()
            .Where(p => p.Status == FinancialPeriodStatus.Open)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync(cancellationToken);

        FinancialPeriodDto? currentPeriod = null;
        if (currentPeriodRow is not null)
        {
            currentPeriod = await _periods.GetPeriodAsync(currentPeriodRow.Id, cancellationToken);
        }

        return new FinanceOverviewDto(
            baseCurrency,
            now,
            currentPeriod,
            FinanceDomain.NormalizeAmount(openReceivables),
            FinanceDomain.NormalizeAmount(openPayables),
            FinanceDomain.NormalizeAmount(cashAndBank),
            trialBalance.TotalDebit,
            trialBalance.TotalCredit,
            trialBalance.Difference,
            draftJournalCount,
            Math.Max(0, cashAccounts.Count - unreconciledCashAccounts));
    }

    private async Task<List<(JournalDirection Direction, decimal BaseAmount)>> LoadAccountLinesAsync(
        Guid accountId,
        DateTimeOffset upTo,
        CancellationToken cancellationToken)
    {
        var lines = await _context.JournalLines
            .AsNoTracking()
            .Where(l => l.FinancialAccountId == accountId &&
                        l.Journal.Status == JournalStatus.Posted &&
                        l.Journal.TransactionDate < upTo)
            .Select(l => new { l.Direction, l.BaseAmount })
            .ToListAsync(cancellationToken);
        return lines.Select(x => (x.Direction, x.BaseAmount)).ToList();
    }

    private async Task<decimal> ComputeBalanceAsync(
        FinancialAccountEntity account,
        DateTimeOffset upTo,
        CancellationToken cancellationToken)
    {
        var lines = await LoadAccountLinesAsync(account.Id, upTo, cancellationToken);
        return FinanceBalanceCalculator.NetBalance(lines, account.NormalBalance);
    }
}
