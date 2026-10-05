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
/// The finance posting engine (D6). Operational events are turned into balanced,
/// posted journals exclusively through the rule mappings configured in
/// <c>financial_posting_rule_mappings</c>, so no account identifier is ever
/// hard-coded in a posting path. B18's tax split is expressed purely by the
/// Gross/Net/Tax selectors on those mappings.
/// </summary>
public sealed class EntityFrameworkFinancePostingService : IFinancePostingService
{
    /// <summary>PostgreSQL unique_violation.</summary>
    private const string UniqueViolation = "23505";

    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;

    public EntityFrameworkFinancePostingService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _clock = clock;
    }

    public Task<JournalDto> PostAsync(
        FinancialPostingRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var rule = _context.FinancialPostingRules
            .AsNoTracking()
            .FirstOrDefault(r => r.Code == request.RuleCode)
            ?? throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("ruleCode", $"Posting rule '{request.RuleCode}' is not configured."));

        if (!rule.IsActive)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("ruleCode", $"Posting rule '{request.RuleCode}' is inactive."));
        }

        return rule.PostingStrategy == FinancialPostingStrategy.Reversal
            ? PostReversalAsync(request, actorUserId, correlationId, cancellationToken)
            : PostDirectAsync(request, rule, actorUserId, correlationId, cancellationToken);
    }

    private async Task<JournalDto> PostDirectAsync(
        FinancialPostingRequest request,
        FinancialPostingRuleEntity rule,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // Phase 8 §15: a replayed operational event returns the journal that already
        // exists instead of posting twice.
        var existing = await FindExistingAsync(request.SourceType, request.SourceId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var mappings = await _context.FinancialPostingRuleMappings
            .AsNoTracking()
            .Include(m => m.FinancialAccount)
            .Where(m => m.RuleId == rule.Id)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);

        if (mappings.Count == 0)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("ruleCode",
                    $"Posting rule '{rule.Code}' has no configured account mappings. Configure them under Finance → Posting rules."));
        }

        var amounts = ResolveAmounts(request, mappings, rule);
        var journal = await BuildJournalAsync(request, actorUserId, correlationId, cancellationToken);
        var exchangeRate = journal.ExchangeRate;

        var lineNumber = 0;
        foreach (var mapping in mappings)
        {
            var amount = FinanceDomain.NormalizeAmount(amounts[mapping.Id]);

            // A zero component (for example a tax-exempt sale) contributes no line, so
            // the mandated "amount > 0" invariant holds without weakening it.
            if (amount == 0m)
            {
                continue;
            }

            if (mapping.FinancialAccount.Status != FinancialAccountStatus.Active)
            {
                throw new ValidationException(
                    "The transaction could not be posted.",
                    FinanceServiceHelpers.FieldError("ruleCode",
                        $"Posting rule '{rule.Code}' maps to inactive account '{mapping.FinancialAccount.Code}'."));
            }

            lineNumber++;
            journal.Lines.Add(BuildLine(journal.Id, lineNumber, mapping.FinancialAccountId, mapping.Direction, amount, request, exchangeRate));
        }

        EnsureBalanced(journal, rule.Code);

        Post(journal, actorUserId);
        _context.Journals.Add(journal);

        return await SaveAndLoadAsync(journal, cancellationToken);
    }

    public async Task<JournalDto> PostReversalAsync(
        FinancialPostingRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindExistingAsync(request.SourceType, request.SourceId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (request.ReversalOfJournalId is not { } originalId)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("reversalOfJournalId",
                    "A reversal posting requires the id of the journal being reversed."));
        }

        var original = await _context.Journals
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == originalId, cancellationToken)
            ?? throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("reversalOfJournalId", $"Journal '{originalId}' does not exist."));

        if (original.Status != JournalStatus.Posted)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("reversalOfJournalId",
                    $"Only a posted journal can be reversed; '{original.Reference}' is {original.Status}."));
        }

        var alreadyReversed = await _context.Journals
            .AsNoTracking()
            .AnyAsync(j => j.ReversalOfJournalId == original.Id, cancellationToken);

        if (alreadyReversed)
        {
            throw new ConflictException($"Journal '{original.Reference}' has already been reversed.");
        }

        // B4: build a NEW compensating journal from the original's lines, inverting
        // every side, and post it into the period of the (open) reversal date.
        var journal = await BuildJournalAsync(request, actorUserId, correlationId, cancellationToken);
        journal.ReversalOfJournalId = original.Id;
        journal.SourceReference = original.Reference;
        journal.ExchangeRate = original.ExchangeRate;
        journal.TransactionCurrencyCode = original.TransactionCurrencyCode;

        var lineNumber = 0;
        foreach (var line in original.Lines.OrderBy(l => l.LineNumber))
        {
            lineNumber++;
            journal.Lines.Add(new JournalLineEntity
            {
                Id = Guid.CreateVersion7(),
                JournalId = journal.Id,
                LineNumber = lineNumber,
                FinancialAccountId = line.FinancialAccountId,
                Direction = FinanceDomain.Invert(line.Direction),
                Amount = line.Amount,
                CurrencyCode = line.CurrencyCode,
                BaseAmount = FinanceDomain.ToBaseAmount(line.Amount, original.ExchangeRate),
                Description = line.Description,
                SourceLineId = line.Id,
                CreatedAtUtc = _clock.UtcNow,
            });
        }

        Post(journal, actorUserId);
        _context.Journals.Add(journal);

        return await SaveAndLoadAsync(journal, cancellationToken);
    }

    /// <summary>
    /// Resolves each mapping's amount from the request components. A mapping with a
    /// selector takes the amount supplied under that selector; a mapping without one
    /// takes the request's single unnamed amount. Every supplied selector must be
    /// consumed, so a posting can never silently drop an amount.
    /// </summary>
    private static Dictionary<Guid, decimal> ResolveAmounts(
        FinancialPostingRequest request,
        IReadOnlyList<FinancialPostingRuleMappingEntity> mappings,
        FinancialPostingRuleEntity rule)
    {
        var bySelector = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var unnamedCount = 0;
        decimal unnamedAmount = 0m;

        foreach (var amount in request.Amounts)
        {
            if (amount.Selector is null)
            {
                unnamedCount++;
                unnamedAmount = amount.Amount;
                continue;
            }

            if (bySelector.ContainsKey(amount.Selector))
            {
                throw new ValidationException(
                    "The transaction could not be posted.",
                    FinanceServiceHelpers.FieldError("amounts",
                        $"The posting request supplies selector '{amount.Selector}' more than once."));
            }

            bySelector[amount.Selector] = amount.Amount;
        }

        var needsUnnamed = mappings.Any(m => m.Selector is null);
        if (needsUnnamed && unnamedCount != 1)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("amounts",
                    $"Posting rule '{rule.Code}' requires exactly one unnamed posting amount, but {unnamedCount} were supplied."));
        }

        var resolved = new Dictionary<Guid, decimal>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings)
        {
            if (mapping.Selector is null)
            {
                resolved[mapping.Id] = unnamedAmount;
                continue;
            }

            if (!bySelector.TryGetValue(mapping.Selector, out var amount))
            {
                throw new ValidationException(
                    "The transaction could not be posted.",
                    FinanceServiceHelpers.FieldError("amounts",
                        $"Posting rule '{rule.Code}' requires a '{mapping.Selector}' amount for its " +
                        $"{mapping.Direction} line on account {mapping.FinancialAccount.Code}, but none was supplied."));
            }

            resolved[mapping.Id] = amount;
            consumed.Add(mapping.Selector);
        }

        var unconsumed = bySelector.Keys.Where(k => !consumed.Contains(k)).ToList();
        if (unconsumed.Count > 0)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("amounts",
                    $"Posting rule '{rule.Code}' does not map the supplied amount(s) " +
                    $"{string.Join(", ", unconsumed)}. Configure the rule mappings before posting."));
        }

        return resolved;
    }

    private async Task<JournalEntity> BuildJournalAsync(
        FinancialPostingRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var currency = FinanceDomain.NormalizeCurrency(request.CurrencyCode);
        var baseCurrency = await FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);
        var exchangeRate = FinanceDomain.ResolveExchangeRate(currency, baseCurrency, request.ExchangeRate);

        // B6: TransactionDate selects the period; the period must be Open.
        var period = await FinancialPeriodResolver.ResolveAsync(
            _context,
            request.TransactionDate,
            null,
            cancellationToken);
        await FinancialPeriodResolver.EnsureOpenAsync(period, cancellationToken);

        return new JournalEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(JournalReference.Prefix, JournalReference.Width, cancellationToken),
            Status = JournalStatus.Draft,
            Description = FinanceServiceHelpers.NormalizeOptional(request.Description) ?? request.RuleCode,
            FinancialPeriodId = period.Id,
            TransactionDate = request.TransactionDate,
            BusinessDate = request.BusinessDate,
            TransactionCurrencyCode = currency,
            BaseCurrencyCode = baseCurrency,
            ExchangeRate = exchangeRate,
            RateDateUtc = request.RateDateUtc ?? request.TransactionDate,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            SourceReference = FinanceServiceHelpers.NormalizeOptional(request.SourceReference),
            ReceivableId = request.ReceivableId,
            PayableId = request.PayableId,
            CreatedByUserId = actorUserId,
            CorrelationId = correlationId ?? string.Empty,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };
    }

    private JournalLineEntity BuildLine(
        Guid journalId,
        int lineNumber,
        Guid accountId,
        JournalDirection direction,
        decimal amount,
        FinancialPostingRequest request,
        decimal exchangeRate)
    {
        return new JournalLineEntity
        {
            Id = Guid.CreateVersion7(),
            JournalId = journalId,
            LineNumber = lineNumber,
            FinancialAccountId = accountId,
            Direction = direction,
            Amount = amount,
            CurrencyCode = FinanceDomain.NormalizeCurrency(request.CurrencyCode),
            BaseAmount = FinanceDomain.ToBaseAmount(amount, exchangeRate),
            CreatedAtUtc = _clock.UtcNow,
        };
    }

    private static void EnsureBalanced(JournalEntity journal, string ruleCode)
    {
        var values = journal.Lines
            .Select(l => new JournalLineValues(l.LineNumber, l.Direction, l.Amount))
            .ToList();

        try
        {
            FinanceDomain.ValidateJournalBalance(values);
        }
        catch (JournalInvariantException exception)
        {
            throw new ValidationException(
                "The transaction could not be posted.",
                FinanceServiceHelpers.FieldError("ruleCode",
                    $"Posting rule '{ruleCode}' produced an unbalanced journal: {exception.Message} " +
                    "Correct the rule mappings for this source."));
        }
    }

    private void Post(JournalEntity journal, string actorUserId)
    {
        // The engine always posts in one step: the journal and its lines are created
        // already Posted, so there is no window in which a half-built posting is
        // visible. Draft journals exist only for the manual journal workflow.
        journal.Status = JournalStatus.Posted;
        journal.PostedAtUtc = _clock.UtcNow;
        journal.PostedByUserId = actorUserId;
    }

    private async Task<JournalDto> SaveAndLoadAsync(JournalEntity journal, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Lost a race on the (SourceType, SourceId) unique index: another worker
            // posted the same source first, so return that journal instead.
            _context.ChangeTracker.Clear();

            var winner = await FindExistingAsync(journal.SourceType, journal.SourceId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }

        return await GetJournalAsync(journal.Id, cancellationToken);
    }

    private async Task<JournalDto?> FindExistingAsync(
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Journals
            .FullJournals()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.SourceType == sourceType && j.SourceId == sourceId, cancellationToken);

        return existing is null ? null : FinanceServiceHelpers.ToJournalDto(existing);
    }

    private async Task<JournalDto> GetJournalAsync(Guid id, CancellationToken cancellationToken)
    {
        var journal = await _context.Journals
            .FullJournals()
            .AsNoTracking()
            .FirstAsync(j => j.Id == id, cancellationToken);

        return FinanceServiceHelpers.ToJournalDto(journal);
    }
}
