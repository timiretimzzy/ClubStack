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
/// B6 period assignment. The journal TransactionDate — never the BusinessDate —
/// selects the financial period. Every posting path funnels through here so the
/// rule cannot be bypassed.
/// </summary>
internal static class FinancialPeriodResolver
{
    /// <summary>
    /// Resolves the period for a transaction date. An explicitly supplied period is
    /// accepted only when it actually contains the transaction date, so a caller
    /// cannot post a transaction into the wrong month.
    /// </summary>
    public static async Task<FinancialPeriodEntity> ResolveAsync(
        ClubStackDbContext context,
        DateTimeOffset transactionDate,
        Guid? explicitPeriodId,
        CancellationToken cancellationToken)
    {
        if (explicitPeriodId is { } requestedId)
        {
            var requested = await context.FinancialPeriods
                .FirstOrDefaultAsync(p => p.Id == requestedId, cancellationToken)
                ?? throw new ValidationException(
                    "The transaction could not be assigned to a financial period.",
                    FinanceServiceHelpers.FieldError("financialPeriodId", $"Financial period '{requestedId}' does not exist."));

            if (transactionDate < requested.StartUtc || transactionDate >= requested.EndUtc)
            {
                throw new ValidationException(
                    "The transaction could not be assigned to a financial period.",
                    FinanceServiceHelpers.FieldError("transactionDate",
                        $"The transaction date {transactionDate:yyyy-MM-dd} is outside period {requested.Reference} " +
                        $"({requested.StartUtc:yyyy-MM-dd} to {requested.EndUtc:yyyy-MM-dd}). " +
                        "D6 PM Decision Pack B6: the TransactionDate determines the financial period."));
            }

            return requested;
        }

        var (year, month) = FinanceDomain.PeriodKeyFor(transactionDate);

        return await context.FinancialPeriods
            .FirstOrDefaultAsync(p => p.Year == year && p.Month == month, cancellationToken)
            ?? throw new ValidationException(
                "The transaction could not be assigned to a financial period.",
                FinanceServiceHelpers.FieldError("transactionDate",
                    $"No financial period is configured for {year}-{month:D2}. Open the period before posting transactions dated in that month."));
    }

    public static async Task EnsureOpenAsync(FinancialPeriodEntity period, CancellationToken cancellationToken)
    {
        if (period.Status != FinancialPeriodStatus.Open)
        {
            throw new ConflictException(
                $"Financial period '{period.Reference}' is {period.Status}. Only an Open period accepts postings.");
        }

        await Task.CompletedTask;
    }
}

/// <inheritdoc />
public sealed class EntityFrameworkFinancialPeriodService : IFinancialPeriodService
{
    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;

    public EntityFrameworkFinancialPeriodService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _clock = clock;
    }

    public async Task<FinancialPeriodSearchResult> SearchPeriodsAsync(
        FinancialPeriodSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<FinancialPeriodEntity> query = _context.FinancialPeriods.AsNoTracking();

        if (input.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (input.Year is { } year)
        {
            query = query.Where(p => p.Year == year);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = await BuildDtosAsync(items, cancellationToken);
        return new FinancialPeriodSearchResult(dtos, total, page, pageSize);
    }

    public async Task<FinancialPeriodDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var period = await _context.FinancialPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Financial period '{id}' was not found.");

        var dto = (await BuildDtosAsync([period], cancellationToken))[0];
        return dto;
    }

    public async Task<FinancialPeriodDto?> ResolveForTransactionDateAsync(
        DateTimeOffset transactionDate,
        CancellationToken cancellationToken = default)
    {
        var (year, month) = FinanceDomain.PeriodKeyFor(transactionDate);

        var period = await _context.FinancialPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Year == year && p.Month == month, cancellationToken);

        if (period is null)
        {
            return null;
        }

        var dto = (await BuildDtosAsync([period], cancellationToken))[0];
        return dto;
    }

    public async Task<FinancialPeriodDto> OpenAsync(
        OpenFinancialPeriodInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (input.Month is < 1 or > 12)
        {
            throw new ValidationException(
                "The financial period could not be opened.",
                FinanceServiceHelpers.FieldError("month", "A month must be between 1 and 12."));
        }

        if (await _context.FinancialPeriods.AnyAsync(p => p.Year == input.Year && p.Month == input.Month, cancellationToken))
        {
            throw new ConflictException($"Financial period {input.Year}-{input.Month:D2} already exists.");
        }

        var start = new DateTimeOffset(input.Year, input.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        var name = FinanceServiceHelpers.NormalizeOptional(input.Name)
                   ?? start.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

        var period = new FinancialPeriodEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(
                FinancialPeriodReference.Prefix,
                FinancialPeriodReference.Width,
                cancellationToken),
            Year = input.Year,
            Month = input.Month,
            Name = name,
            StartUtc = start,
            EndUtc = end,
            Status = FinancialPeriodStatus.Open,
            OpenedAtUtc = _clock.UtcNow,
            OpenedByUserId = actorUserId,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        _context.FinancialPeriods.Add(period);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = (await BuildDtosAsync([period], cancellationToken))[0];
        return dto;
    }

    public async Task<FinancialPeriodDto> CloseAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var period = await _context.FinancialPeriods
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Financial period '{id}' was not found.");

        if (period.Status == FinancialPeriodStatus.Closed)
        {
            throw new ConflictException($"Financial period '{period.Reference}' is already closed.");
        }

        // D6 close checks: no draft journal may be left unresolved in the period, and
        // the period must actually hold posted journals (a period is closed because it
        // has been used).
        var (draftCount, postedCount, debitTotal, creditTotal) = await SummarizeAsync(period.Id, cancellationToken);

        if (draftCount > 0)
        {
            throw new ConflictException(
                $"Financial period '{period.Reference}' cannot be closed while {draftCount} draft " +
                $"journal(s) remain. Post or cancel them first.");
        }

        if (postedCount == 0)
        {
            throw new ConflictException(
                $"Financial period '{period.Reference}' cannot be closed because it has no posted journals.");
        }

        if (debitTotal != creditTotal)
        {
            throw new ConflictException(
                $"Financial period '{period.Reference}' cannot be closed: debits {debitTotal} do not equal credits {creditTotal}.");
        }

        period.Status = FinancialPeriodStatus.Closed;
        period.ClosedAtUtc = _clock.UtcNow;
        period.ClosedByUserId = actorUserId;
        period.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = (await BuildDtosAsync([period], cancellationToken))[0];
        return dto;
    }

    public async Task<FinancialPeriodDto> ReopenAsync(
        Guid id,
        ReopenFinancialPeriodInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var period = await _context.FinancialPeriods
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Financial period '{id}' was not found.");

        if (period.Status != FinancialPeriodStatus.Closed)
        {
            throw new ConflictException($"Only a Closed financial period can be reopened; '{period.Reference}' is {period.Status}.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The financial period could not be reopened.",
                FinanceServiceHelpers.FieldError("reason", "A reason is required to reopen a financial period."));
        }

        // B5: only the most recently closed period may be reopened. A later period that
        // has already been closed must be reopened first, otherwise the ledger would
        // accept out-of-order postings.
        var latestClosed = await _context.FinancialPeriods
            .Where(p => p.Status == FinancialPeriodStatus.Closed)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestClosed is null || latestClosed.Id != period.Id)
        {
            var latestReference = latestClosed?.Reference ?? "none";
            throw new ConflictException(
                $"Only the most recently closed financial period can be reopened. '{period.Reference}' is closed but " +
                $"'{latestReference}' was closed later.");
        }

        period.Status = FinancialPeriodStatus.Open;
        period.ReopenedAtUtc = _clock.UtcNow;
        period.ReopenedByUserId = actorUserId;
        period.ReopenReason = input.Reason.Trim();
        period.ReopenCount += 1;
        period.ClosedAtUtc = null;
        period.ClosedByUserId = null;
        period.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = (await BuildDtosAsync([period], cancellationToken))[0];
        return dto;
    }

    private async Task<(int DraftCount, int PostedCount, decimal DebitTotal, decimal CreditTotal)> SummarizeAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var draftCount = await _context.Journals
            .CountAsync(j => j.FinancialPeriodId == periodId && j.Status == JournalStatus.Draft, cancellationToken);

        var postedLines = await _context.JournalLines
            .AsNoTracking()
            .Where(l => l.Journal.FinancialPeriodId == periodId && l.Journal.Status == JournalStatus.Posted)
            .Select(l => new { l.Direction, l.BaseAmount })
            .ToListAsync(cancellationToken);

        var debit = FinanceDomain.NormalizeAmount(
            postedLines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.BaseAmount));
        var credit = FinanceDomain.NormalizeAmount(
            postedLines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.BaseAmount));

        var postedCount = await _context.Journals
            .CountAsync(j => j.FinancialPeriodId == periodId && j.Status == JournalStatus.Posted, cancellationToken);

        return (draftCount, postedCount, debit, credit);
    }

    private async Task<IReadOnlyList<FinancialPeriodDto>>
        BuildDtosAsync(IReadOnlyList<FinancialPeriodEntity> periods, CancellationToken cancellationToken)
    {
        if (periods.Count == 0)
        {
            return [];
        }

        var ids = periods.Select(p => p.Id).ToList();

        var postedLines = await _context.JournalLines
            .AsNoTracking()
            .Where(l => ids.Contains(l.Journal.FinancialPeriodId) && l.Journal.Status == JournalStatus.Posted)
            .Select(l => new { l.Journal.FinancialPeriodId, l.Direction, l.BaseAmount })
            .ToListAsync(cancellationToken);

        var counts = await _context.Journals
            .AsNoTracking()
            .Where(j => ids.Contains(j.FinancialPeriodId) && j.Status == JournalStatus.Posted)
            .GroupBy(j => j.FinancialPeriodId)
            .Select(g => new { PeriodId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var postedJournalCounts = counts.ToDictionary(c => c.PeriodId, c => c.Count);

        var totals = postedLines
            .GroupBy(l => l.FinancialPeriodId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Count: postedJournalCounts.TryGetValue(g.Key, out var postedCount) ? postedCount : 0,
                    Debit: FinanceDomain.NormalizeAmount(g.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.BaseAmount)),
                    Credit: FinanceDomain.NormalizeAmount(g.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.BaseAmount))));

        var items = periods
            .Select(p =>
            {
                totals.TryGetValue(p.Id, out var t);
                return new FinancialPeriodDto(
                    p.Id,
                    p.Reference,
                    p.Year,
                    p.Month,
                    p.Name,
                    p.StartUtc,
                    p.EndUtc,
                    p.Status,
                    p.OpenedAtUtc,
                    p.OpenedByUserId,
                    p.ClosedAtUtc,
                    p.ClosedByUserId,
                    p.ReopenedAtUtc,
                    p.ReopenedByUserId,
                    p.ReopenReason,
                    t.Count,
                    t.Debit,
                    t.Credit);
            })
            .ToList();

        return items;
    }
}
