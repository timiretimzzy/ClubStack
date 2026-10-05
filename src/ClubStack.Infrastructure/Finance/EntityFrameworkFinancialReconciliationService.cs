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
/// Manual reconciliation (B15). There is no bank API and no automatic statement
/// import: a user records the statement closing balance, matches ledger journals by
/// hand, and finalizes. The ledger closing balance and the variance are always
/// derived from posted journals, never typed in by the user.
/// </summary>
public sealed class EntityFrameworkFinancialReconciliationService : IFinancialReconciliationService
{
    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;

    public EntityFrameworkFinancialReconciliationService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _clock = clock;
    }

    public async Task<FinancialReconciliationSearchResult> SearchAsync(
        FinancialReconciliationSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<FinancialReconciliationEntity> query = _context.FinancialReconciliations
            .Include(r => r.FinancialAccount)
            .Include(r => r.Matches).ThenInclude(m => m.Journal)
            .AsNoTracking();

        if (input.FinancialAccountId is { } accountId)
        {
            query = query.Where(r => r.FinancialAccountId == accountId);
        }

        if (input.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StatementDateUtc)
            .ThenBy(r => r.Reference)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new FinancialReconciliationSearchResult(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<FinancialReconciliationDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reconciliation = await _context.FinancialReconciliations
            .Include(r => r.FinancialAccount)
            .Include(r => r.Matches).ThenInclude(m => m.Journal)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Reconciliation '{id}' was not found.");

        return ToDto(reconciliation);
    }

    public async Task<FinancialReconciliationDto> CreateAsync(
        CreateReconciliationInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == input.FinancialAccountId, cancellationToken)
            ?? throw new ValidationException(
                "The reconciliation could not be created.",
                FinanceServiceHelpers.FieldError("financialAccountId",
                    $"Financial account '{input.FinancialAccountId}' does not exist."));

        if (account.Status != FinancialAccountStatus.Active)
        {
            throw new ValidationException(
                "The reconciliation could not be created.",
                FinanceServiceHelpers.FieldError("financialAccountId",
                    $"Financial account '{account.Code}' ({account.Name}) is inactive."));
        }

        if (!FinanceDomain.FitsAmountPrecision(input.StatementClosingBalance))
        {
            throw new ValidationException(
                "The reconciliation could not be created.",
                FinanceServiceHelpers.FieldError("statementClosingBalance",
                    "The statement closing balance exceeds the supported decimal(19,4) precision."));
        }

        var reconciliation = new FinancialReconciliationEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(ReconciliationReference.Prefix, ReconciliationReference.Width, cancellationToken),
            FinancialAccountId = account.Id,
            StatementDateUtc = input.StatementDateUtc,
            StatementClosingBalance = FinanceDomain.NormalizeAmount(input.StatementClosingBalance),
            LedgerClosingBalance = await ComputeLedgerClosingBalanceAsync(account, input.StatementDateUtc, cancellationToken),
            Status = FinancialReconciliationStatus.InProgress,
            Note = FinanceServiceHelpers.NormalizeOptional(input.Note),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        reconciliation.Variance = Variance(reconciliation);

        _context.FinancialReconciliations.Add(reconciliation);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetAsync(reconciliation.Id, cancellationToken);
    }

    public async Task<FinancialReconciliationDto> AddMatchAsync(
        Guid id,
        MatchReconciliationLineInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireOpenAsync(id, cancellationToken);

        var journal = await _context.Journals
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == input.JournalId, cancellationToken)
            ?? throw new ValidationException(
                "The match could not be recorded.",
                FinanceServiceHelpers.FieldError("journalId", $"Journal '{input.JournalId}' does not exist."));

        if (journal.Status != JournalStatus.Posted)
        {
            throw new ValidationException(
                "The match could not be recorded.",
                FinanceServiceHelpers.FieldError("journalId",
                    $"Journal '{journal.Reference}' is {journal.Status}; only a posted journal can be matched."));
        }

        // A match is only meaningful when the journal actually touches the account
        // under reconciliation.
        var accountLines = journal.Lines
            .Where(l => l.FinancialAccountId == reconciliation.FinancialAccountId)
            .ToList();

        if (accountLines.Count == 0)
        {
            throw new ValidationException(
                "The match could not be recorded.",
                FinanceServiceHelpers.FieldError("journalId",
                    $"Journal '{journal.Reference}' has no line on account '{reconciliation.FinancialAccount.Code}'."));
        }

        if (await _context.FinancialReconciliationMatches
                .AnyAsync(m => m.ReconciliationId == id && m.JournalId == input.JournalId, cancellationToken))
        {
            throw new ConflictException(
                $"Journal '{journal.Reference}' is already matched to reconciliation '{reconciliation.Reference}'.");
        }

        if (!FinanceDomain.FitsAmountPrecision(input.LedgerAmount))
        {
            throw new ValidationException(
                "The match could not be recorded.",
                FinanceServiceHelpers.FieldError("ledgerAmount",
                    "The ledger amount exceeds the supported decimal(19,4) precision."));
        }

        _context.FinancialReconciliationMatches.Add(new FinancialReconciliationMatchEntity
        {
            Id = Guid.CreateVersion7(),
            ReconciliationId = reconciliation.Id,
            JournalId = journal.Id,
            LedgerAmount = FinanceDomain.NormalizeAmount(input.LedgerAmount),
            Note = FinanceServiceHelpers.NormalizeOptional(input.Note),
            MatchedAtUtc = _clock.UtcNow,
            MatchedByUserId = actorUserId,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<FinancialReconciliationDto> RemoveMatchAsync(
        Guid id,
        Guid matchId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireOpenAsync(id, cancellationToken);

        var match = await _context.FinancialReconciliationMatches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.ReconciliationId == id, cancellationToken)
            ?? throw new NotFoundException($"Reconciliation match '{matchId}' was not found.");

        _context.FinancialReconciliationMatches.Remove(match);
        await _context.SaveChangesAsync(cancellationToken);

        _ = actorUserId;
        return await GetAsync(id, cancellationToken);
    }

    public async Task<FinancialReconciliationDto> CompleteAsync(
        Guid id,
        CompleteReconciliationInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireOpenAsync(id, cancellationToken);

        // Freeze the derived figures against the ledger as it stands at finalization.
        var account = await _context.FinancialAccounts
            .AsNoTracking()
            .FirstAsync(a => a.Id == reconciliation.FinancialAccountId, cancellationToken);

        reconciliation.LedgerClosingBalance = await ComputeLedgerClosingBalanceAsync(
            account,
            reconciliation.StatementDateUtc,
            cancellationToken);

        reconciliation.Variance = Variance(reconciliation);
        reconciliation.Status = FinancialReconciliationStatus.Completed;
        reconciliation.CompletedAtUtc = _clock.UtcNow;
        reconciliation.CompletedByUserId = actorUserId;
        reconciliation.UpdatedAtUtc = _clock.UtcNow;

        if (input.Note is not null)
        {
            reconciliation.Note = FinanceServiceHelpers.NormalizeOptional(input.Note);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<FinancialReconciliationDto> CancelAsync(
        Guid id,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireOpenAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                "The reconciliation could not be cancelled.",
                FinanceServiceHelpers.FieldError("reason", "A cancellation reason is required."));
        }

        reconciliation.Status = FinancialReconciliationStatus.Cancelled;
        reconciliation.CancelledReason = reason.Trim();
        reconciliation.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _ = actorUserId;
        return await GetAsync(id, cancellationToken);
    }

    private async Task<FinancialReconciliationEntity> RequireOpenAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reconciliation = await _context.FinancialReconciliations
            .Include(r => r.FinancialAccount)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Reconciliation '{id}' was not found.");

        if (reconciliation.Status != FinancialReconciliationStatus.InProgress)
        {
            throw new ConflictException(
                $"Reconciliation '{reconciliation.Reference}' is {reconciliation.Status} and can no longer be modified.");
        }

        return reconciliation;
    }

    /// <summary>
    /// B15: the ledger closing balance is the posted net balance of the account up to
    /// the statement date, in the account's own normal-balance terms.
    /// </summary>
    private async Task<decimal> ComputeLedgerClosingBalanceAsync(
        FinancialAccountEntity account,
        DateTimeOffset statementDate,
        CancellationToken cancellationToken)
    {
        var lines = await _context.JournalLines
            .AsNoTracking()
            .Where(l => l.FinancialAccountId == account.Id &&
                        l.Journal.Status == JournalStatus.Posted &&
                        l.Journal.TransactionDate <= statementDate)
            .Select(l => new { l.Direction, l.BaseAmount })
            .ToListAsync(cancellationToken);

        return FinanceBalanceCalculator.NetBalance(
            lines.Select(l => (l.Direction, l.BaseAmount)).ToList(),
            account.NormalBalance);
    }

    private static decimal? Variance(FinancialReconciliationEntity reconciliation) =>
        reconciliation.LedgerClosingBalance is null
            ? null
            : FinanceDomain.NormalizeAmount(
                reconciliation.StatementClosingBalance - reconciliation.LedgerClosingBalance.Value);

    internal static FinancialReconciliationDto ToDto(FinancialReconciliationEntity r) => new(
        r.Id,
        r.Reference,
        r.FinancialAccountId,
        r.FinancialAccount.Code,
        r.FinancialAccount.Name,
        r.StatementDateUtc,
        r.StatementClosingBalance,
        r.LedgerClosingBalance,
        r.Variance,
        r.Status,
        r.Note,
        r.CreatedByUserId,
        r.CreatedAtUtc,
        r.CompletedAtUtc,
        r.CompletedByUserId,
        r.CancelledReason,
        r.Matches
            .OrderBy(m => m.MatchedAtUtc)
            .Select(m => new ReconciliationMatchDto(
                m.Id,
                m.JournalId,
                m.Journal.Reference,
                m.Journal.TransactionDate,
                m.LedgerAmount,
                m.Note,
                m.MatchedAtUtc,
                m.MatchedByUserId))
            .ToList(),
        FinanceDomain.NormalizeAmount(r.Matches.Sum(m => m.LedgerAmount)));
}
