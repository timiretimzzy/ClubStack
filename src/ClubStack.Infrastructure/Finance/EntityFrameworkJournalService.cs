using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Finance;

/// <inheritdoc />
public sealed class EntityFrameworkJournalService : IJournalService
{
    /// <summary>Source type recorded on a manually created journal.</summary>
    public const string ManualSourceType = "ManualJournal";

    /// <summary>Source type recorded on the B4 compensating journal of a reversal.</summary>
    public const string ReversalSourceType = "JournalReversal";

    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;

    public EntityFrameworkJournalService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _clock = clock;
    }

    public async Task<JournalSearchResult> SearchJournalsAsync(
        JournalSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<JournalEntity> query = _context.Journals.FullJournals().AsNoTracking();

        if (input.Status is { } status)
        {
            query = query.Where(j => j.Status == status);
        }

        if (input.FinancialPeriodId is { } periodId)
        {
            query = query.Where(j => j.FinancialPeriodId == periodId);
        }

        if (input.SourceType is not null)
        {
            var sourceType = input.SourceType.Trim();
            query = query.Where(j => j.SourceType == sourceType);
        }

        if (input.FinancialAccountId is { } accountId)
        {
            query = query.Where(j => j.Lines.Any(l => l.FinancialAccountId == accountId));
        }

        if (input.FromDate is { } from)
        {
            query = query.Where(j => j.TransactionDate >= from);
        }

        if (input.ToDate is { } to)
        {
            query = query.Where(j => j.TransactionDate < to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(j => j.TransactionDate)
            .ThenByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new JournalSearchResult(
            items.Select(FinanceServiceHelpers.ToJournalDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<JournalDto> GetJournalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var journal = await _context.Journals
            .FullJournals()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Journal '{id}' was not found.");

        return FinanceServiceHelpers.ToJournalDto(journal);
    }

    public async Task<JournalDto> GetJournalByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var journal = await _context.Journals
            .FullJournals()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Reference == reference, cancellationToken)
            ?? throw new NotFoundException($"Journal '{reference}' was not found.");

        return FinanceServiceHelpers.ToJournalDto(journal);
    }

    public async Task<JournalDto> CreateDraftAsync(
        CreateJournalInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Description))
        {
            throw new ValidationException(
                "The journal could not be saved.",
                FinanceServiceHelpers.FieldError("description", "A journal description is required."));
        }

        if (input.Lines is null || input.Lines.Count < 2)
        {
            throw new ValidationException(
                "The journal could not be saved.",
                FinanceServiceHelpers.FieldError("lines", "A journal requires at least two lines (D6 PM Decision Pack B9)."));
        }

        var currency = FinanceDomain.NormalizeCurrency(input.CurrencyCode);
        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);
        var exchangeRate = FinanceDomain.ResolveExchangeRate(currency, baseCurrency, input.ExchangeRate);
        var rateDate = input.RateDateUtc ?? input.TransactionDate;

        // B6: the TransactionDate decides the period. A caller-supplied period must
        // contain that date.
        var period = await FinancialPeriodResolver.ResolveAsync(
            _context,
            input.TransactionDate,
            input.FinancialPeriodId,
            cancellationToken);

        var accountIds = input.Lines.Select(l => l.FinancialAccountId).Distinct().ToList();
        var accounts = await _context.FinancialAccounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var line in input.Lines)
        {
            if (!accounts.TryGetValue(line.FinancialAccountId, out var account))
            {
                throw new ValidationException(
                    "The journal could not be saved.",
                    FinanceServiceHelpers.FieldError("lines", $"Financial account '{line.FinancialAccountId}' does not exist."));
            }

            if (account.Status != FinancialAccountStatus.Active)
            {
                throw new ValidationException(
                    "The journal could not be saved.",
                    FinanceServiceHelpers.FieldError("lines", $"Financial account '{account.Code}' is inactive."));
            }

            if (!account.IsPostingAllowed)
            {
                throw new ValidationException(
                    "The journal could not be saved.",
                    FinanceServiceHelpers.FieldError("lines", $"Financial account '{account.Code}' does not accept postings."));
            }
        }

        var lineValues = input.Lines
            .Select((l, i) => new JournalLineValues(i + 1, l.Direction, FinanceDomain.NormalizeAmount(l.Amount)))
            .ToList();

        try
        {
            FinanceDomain.ValidateJournalBalance(lineValues);
        }
        catch (JournalInvariantException exception)
        {
            throw new ValidationException(
                "The journal could not be saved.",
                FinanceServiceHelpers.FieldError("lines", exception.Message));
        }

        var journal = new JournalEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(JournalReference.Prefix, JournalReference.Width, cancellationToken),
            Status = JournalStatus.Draft,
            Description = input.Description.Trim(),
            FinancialPeriodId = period.Id,
            TransactionDate = input.TransactionDate,
            BusinessDate = input.BusinessDate ?? input.TransactionDate,
            TransactionCurrencyCode = currency,
            BaseCurrencyCode = baseCurrency,
            ExchangeRate = exchangeRate,
            RateDateUtc = rateDate,
            SourceType = ManualSourceType,
            // A manual journal's source identity is its own id, so the Phase 8 §15
            // unique source index can never collide with another journal.
            SourceId = Guid.CreateVersion7(),
            SourceReference = FinanceServiceHelpers.NormalizeOptional(input.SourceReference),
            ReversalOfJournalId = input.ReversalOfJournalId,
            CreatedByUserId = actorUserId,
            CorrelationId = string.Empty,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        if (input.ReversalOfJournalId is { } originalId)
        {
            _ = await _context.Journals
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == originalId, cancellationToken)
                ?? throw new ValidationException(
                    "The journal could not be saved.",
                    FinanceServiceHelpers.FieldError("reversalOfJournalId", $"Journal '{originalId}' does not exist."));
        }

        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            var amount = FinanceDomain.NormalizeAmount(line.Amount);

            journal.Lines.Add(new JournalLineEntity
            {
                Id = Guid.CreateVersion7(),
                JournalId = journal.Id,
                LineNumber = i + 1,
                FinancialAccountId = line.FinancialAccountId,
                Direction = line.Direction,
                Amount = amount,
                CurrencyCode = currency,
                BaseAmount = FinanceDomain.ToBaseAmount(amount, exchangeRate),
                Description = FinanceServiceHelpers.NormalizeOptional(line.Description),
                CreatedAtUtc = _clock.UtcNow,
            });
        }

        _context.Journals.Add(journal);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetJournalAsync(journal.Id, cancellationToken);
    }

    public async Task<JournalValidationResult> ValidateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var journal = await _context.Journals
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Journal '{id}' was not found.");

        var errors = new List<string>();
        var lines = journal.Lines.OrderBy(l => l.LineNumber).ToList();

        if (lines.Count < 2)
        {
            errors.Add($"A journal requires at least two lines (B9); {lines.Count} present.");
        }

        if (!lines.Any(l => l.Direction == JournalDirection.Debit))
        {
            errors.Add("The journal has no debit line.");
        }

        if (!lines.Any(l => l.Direction == JournalDirection.Credit))
        {
            errors.Add("The journal has no credit line.");
        }

        var totalDebit = FinanceDomain.NormalizeAmount(lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.Amount));
        var totalCredit = FinanceDomain.NormalizeAmount(lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.Amount));

        if (totalDebit != totalCredit)
        {
            errors.Add($"Debits {totalDebit} do not equal credits {totalCredit}.");
        }

        if (lines.Any(l => l.Amount <= 0m))
        {
            errors.Add("Every line must have an amount greater than zero.");
        }

        var period = await _context.FinancialPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == journal.FinancialPeriodId, cancellationToken);

        if (period is null)
        {
            errors.Add("The journal is not assigned to a financial period.");
        }
        else
        {
            if (period.Status != FinancialPeriodStatus.Open)
            {
                errors.Add($"Period {period.Reference} is {period.Status}; only an Open period accepts postings.");
            }

            if (journal.TransactionDate < period.StartUtc || journal.TransactionDate >= period.EndUtc)
            {
                errors.Add(
                    $"The transaction date {journal.TransactionDate:yyyy-MM-dd} is outside period {period.Reference} (B6).");
            }
        }

        return new JournalValidationResult(errors.Count == 0, errors, totalDebit, totalCredit, lines.Count);
    }

    public async Task<JournalDto> PostAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var journal = await _context.Journals
            .Include(j => j.Lines)
            .Include(j => j.FinancialPeriod)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Journal '{id}' was not found.");

        if (journal.Status == JournalStatus.Posted)
        {
            throw new ConflictException($"Journal '{journal.Reference}' is already posted.");
        }

        if (journal.Status == JournalStatus.Cancelled)
        {
            throw new ConflictException($"Journal '{journal.Reference}' is cancelled and cannot be posted.");
        }

        var validation = await ValidateAsync(id, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(
                $"Journal '{journal.Reference}' cannot be posted: {string.Join(" ", validation.Errors)}",
                new Dictionary<string, string[]> { ["lines"] = validation.Errors.ToArray() });
        }

        // B1: Draft → Posted. From here the journal (and its lines) are immutable; the
        // database also refuses any later UPDATE/DELETE on a posted journal.
        journal.Status = JournalStatus.Posted;
        journal.PostedAtUtc = _clock.UtcNow;
        journal.PostedByUserId = actorUserId;
        journal.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetJournalAsync(id, cancellationToken);
    }

    public async Task<JournalDto> CancelAsync(
        Guid id,
        CancelJournalInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var journal = await _context.Journals
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Journal '{id}' was not found.");

        if (journal.Status != JournalStatus.Draft)
        {
            throw new ConflictException(
                $"Only a Draft journal can be cancelled; '{journal.Reference}' is {journal.Status} (D6 PM Decision Pack B1).");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The journal could not be cancelled.",
                FinanceServiceHelpers.FieldError("reason", "A reason is required to cancel a journal."));
        }

        journal.Status = JournalStatus.Cancelled;
        journal.CancelledAtUtc = _clock.UtcNow;
        journal.CancelledReason = input.Reason.Trim();
        journal.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetJournalAsync(id, cancellationToken);
    }

    public async Task<JournalDto> ReverseAsync(
        Guid id,
        ReverseJournalInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var original = await _context.Journals
            .FullJournals()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Journal '{id}' was not found.");

        if (original.Status != JournalStatus.Posted)
        {
            throw new ConflictException(
                $"Only a posted journal can be reversed; '{original.Reference}' is {original.Status} (B4).");
        }

        // A posted journal may be corrected by exactly one compensating journal; a
        // second reversal of the same original would duplicate the same correction.
        var existingReversal = await _context.Journals
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.ReversalOfJournalId == original.Id, cancellationToken);

        if (existingReversal is not null)
        {
            throw new ConflictException(
                $"Journal '{original.Reference}' has already been reversed by '{existingReversal.Reference}'.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The journal could not be reversed.",
                FinanceServiceHelpers.FieldError("reason", "A reason is required to reverse a journal."));
        }

        // B4/B19: the reversal is a NEW journal that inverts every line and posts into
        // an open period. The original is never modified.
        var transactionDate = input.TransactionDate ?? _clock.UtcNow;
        var period = await FinancialPeriodResolver.ResolveAsync(_context, transactionDate, null, cancellationToken);
        await FinancialPeriodResolver.EnsureOpenAsync(period, cancellationToken);

        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);
        var exchangeRate = FinanceDomain.ResolveExchangeRate(
            original.TransactionCurrencyCode,
            baseCurrency,
            original.ExchangeRate);

        var reversal = new JournalEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(JournalReference.Prefix, JournalReference.Width, cancellationToken),
            Status = JournalStatus.Posted,
            Description = $"Reversal of {original.Reference}: {input.Reason.Trim()}",
            FinancialPeriodId = period.Id,
            TransactionDate = transactionDate,
            BusinessDate = original.BusinessDate,
            TransactionCurrencyCode = original.TransactionCurrencyCode,
            BaseCurrencyCode = baseCurrency,
            ExchangeRate = exchangeRate,
            RateDateUtc = _clock.UtcNow,
            SourceType = ReversalSourceType,
            SourceId = original.Id,
            SourceReference = original.Reference,
            ReversalOfJournalId = original.Id,
            ReceivableId = original.ReceivableId,
            PayableId = original.PayableId,
            PostedAtUtc = _clock.UtcNow,
            PostedByUserId = actorUserId,
            CreatedByUserId = actorUserId,
            CorrelationId = original.CorrelationId,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        var lineNumber = 0;
        foreach (var line in original.Lines.OrderBy(l => l.LineNumber))
        {
            lineNumber++;
            reversal.Lines.Add(new JournalLineEntity
            {
                Id = Guid.CreateVersion7(),
                JournalId = reversal.Id,
                LineNumber = lineNumber,
                FinancialAccountId = line.FinancialAccountId,
                // B4: invert every debit/credit line.
                Direction = FinanceDomain.Invert(line.Direction),
                Amount = line.Amount,
                CurrencyCode = line.CurrencyCode,
                BaseAmount = FinanceDomain.ToBaseAmount(line.Amount, exchangeRate),
                Description = line.Description,
                // B8: the reversal line points at the line it compensates.
                SourceLineId = line.Id,
                CreatedAtUtc = _clock.UtcNow,
            });
        }

        _context.Journals.Add(reversal);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetJournalAsync(reversal.Id, cancellationToken);
    }
}
