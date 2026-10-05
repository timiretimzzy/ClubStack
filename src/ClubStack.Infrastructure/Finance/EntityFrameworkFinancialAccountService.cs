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
public sealed class EntityFrameworkFinancialAccountService : IFinancialAccountService
{
    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _references;
    private readonly ISystemClock _clock;

    public EntityFrameworkFinancialAccountService(
        ClubStackDbContext context,
        IReferenceGenerator references,
        ISystemClock clock)
    {
        _context = context;
        _references = references;
        _clock = clock;
    }

    public async Task<FinancialAccountSearchResult> SearchAccountsAsync(
        FinancialAccountSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = FinanceServiceHelpers.NormalizePage(input.Page, input.PageSize);
        IQueryable<FinancialAccountEntity> query = _context.FinancialAccounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var code = input.Code.Trim();
            query = query.Where(a => EF.Functions.ILike(a.Code, $"%{code}%"));
        }

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            var name = input.Name.Trim();
            query = query.Where(a => EF.Functions.ILike(a.Name, $"%{name}%"));
        }

        if (input.AccountType is { } accountType)
        {
            query = query.Where(a => a.AccountType == accountType);
        }

        if (input.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(a => a.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new FinancialAccountSearchResult(
            items.Select(FinanceServiceHelpers.ToAccountDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<FinancialAccountDto> GetAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Financial account '{id}' was not found.");

        return FinanceServiceHelpers.ToAccountDto(account);
    }

    public async Task<IReadOnlyList<FinancialAccountDto>> GetSystemAccountsAsync(CancellationToken cancellationToken = default) =>
        await _context.FinancialAccounts
            .AsNoTracking()
            .Where(a => a.IsSystem && a.Status == FinancialAccountStatus.Active)
            .OrderBy(a => a.Code)
            .Select(a => FinanceServiceHelpers.ToAccountDto(a))
            .ToListAsync(cancellationToken);

    public async Task<FinancialAccountDto> CreateAccountAsync(
        CreateFinancialAccountInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var code = (input.Code ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(
                "The account could not be created.",
                FinanceServiceHelpers.FieldError("code", "An account code is required."));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The account could not be created.",
                FinanceServiceHelpers.FieldError("name", "An account name is required."));
        }

        if (await _context.FinancialAccounts.AnyAsync(a => a.Code == code, cancellationToken))
        {
            throw new ConflictException($"An account with code '{code}' already exists.");
        }

        if (input.ParentAccountId is { } parentId)
        {
            _ = await _context.FinancialAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == parentId, cancellationToken)
                ?? throw new ValidationException(
                    "The account could not be created.",
                    FinanceServiceHelpers.FieldError("parentAccountId", $"Parent account '{parentId}' does not exist."));
        }

        var currency = FinanceDomain.NormalizeCurrency(input.CurrencyCode);
        await FinanceServiceHelpers.RequireKnownCurrencyAsync(_context, currency, cancellationToken);

        var account = new FinancialAccountEntity
        {
            Id = Guid.CreateVersion7(),
            Reference = await _references.GenerateAsync(FinancialAccountReference.Prefix, FinancialAccountReference.Width, cancellationToken),
            Code = code,
            Name = input.Name.Trim(),
            // B3: the normal balance is derived from the account type, never chosen freely.
            AccountType = input.AccountType,
            NormalBalance = FinanceDomain.NormalBalanceFor(input.AccountType),
            Status = FinancialAccountStatus.Active,
            ParentAccountId = input.ParentAccountId,
            Description = FinanceServiceHelpers.NormalizeOptional(input.Description),
            IsSystem = false,
            IsPostingAllowed = input.IsPostingAllowed,
            CashKind = input.CashKind,
            CurrencyCode = currency,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };

        _context.FinancialAccounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        return FinanceServiceHelpers.ToAccountDto(account);
    }

    public async Task<FinancialAccountDto> UpdateAccountAsync(
        Guid id,
        UpdateFinancialAccountInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.FinancialAccounts
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Financial account '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The account could not be updated.",
                FinanceServiceHelpers.FieldError("name", "An account name is required."));
        }

        if (input.ParentAccountId == account.Id)
        {
            throw new ValidationException(
                "The account could not be updated.",
                FinanceServiceHelpers.FieldError("parentAccountId", "An account cannot be its own parent."));
        }

        account.Name = input.Name.Trim();
        account.ParentAccountId = input.ParentAccountId;
        account.Description = FinanceServiceHelpers.NormalizeOptional(input.Description);
        account.IsPostingAllowed = input.IsPostingAllowed;
        account.CashKind = input.CashKind;
        account.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return FinanceServiceHelpers.ToAccountDto(account);
    }

    public async Task<FinancialAccountDto> DeactivateAccountAsync(
        Guid id,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.FinancialAccounts
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Financial account '{id}' was not found.");

        if (account.Status == FinancialAccountStatus.Inactive)
        {
            return FinanceServiceHelpers.ToAccountDto(account);
        }

        // Inactivation, never destructive deletion: posted journals keep referencing the account.
        account.Status = FinancialAccountStatus.Inactive;
        account.IsPostingAllowed = false;
        account.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return FinanceServiceHelpers.ToAccountDto(account);
    }

    public Task<string> GetBaseCurrencyAsync(CancellationToken cancellationToken = default) =>
        FinanceServiceHelpers.GetBaseCurrencyAsync(_context, cancellationToken);

    public async Task SetBaseCurrencyAsync(
        string currencyCode,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var currency = FinanceDomain.NormalizeCurrency(currencyCode);
        await FinanceServiceHelpers.RequireKnownCurrencyAsync(_context, currency, cancellationToken);
        await FinanceServiceHelpers.SetBaseCurrencyAsync(_context, _clock, currency, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialPostingRuleDto>> GetPostingRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _context.FinancialPostingRules
            .AsNoTracking()
            .Include(r => r.Mappings).ThenInclude(m => m.FinancialAccount)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

        return rules
            .Select(r => new FinancialPostingRuleDto(
                r.Id,
                r.Code,
                r.Name,
                r.SourceSection,
                r.PostingStrategy,
                r.Description,
                r.Mappings
                    .OrderBy(m => m.Sequence)
                    .Select(m => new FinancialPostingRuleMappingDto(
                        m.Id,
                        m.RuleId,
                        r.Code,
                        m.Sequence,
                        m.Direction,
                        m.FinancialAccountId,
                        m.FinancialAccount.Code,
                        m.FinancialAccount.Name,
                        m.Selector))
                    .ToList()))
            .ToList();
    }

    public async Task<FinancialPostingRuleMappingDto> UpsertPostingRuleMappingAsync(
        string ruleCode,
        UpsertPostingRuleMappingInput input,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _context.FinancialPostingRules
            .Include(r => r.Mappings)
            .FirstOrDefaultAsync(r => r.Code == ruleCode, cancellationToken)
            ?? throw new NotFoundException($"Posting rule '{ruleCode}' was not found.");

        var account = await FinanceServiceHelpers.RequirePostableAccountAsync(
            _context,
            input.FinancialAccountId,
            cancellationToken);

        var selector = FinanceServiceHelpers.NormalizeOptional(input.Selector);
        if (selector is { Length: > 64 })
        {
            throw new ValidationException(
                "The posting rule mapping could not be saved.",
                FinanceServiceHelpers.FieldError("selector", "A selector cannot exceed 64 characters."));
        }

        var existing = rule.Mappings.FirstOrDefault(m => m.Sequence == input.Sequence);

        if (existing is null)
        {
            existing = new FinancialPostingRuleMappingEntity
            {
                Id = Guid.CreateVersion7(),
                RuleId = rule.Id,
                Sequence = input.Sequence,
                CreatedAtUtc = _clock.UtcNow,
            };
            _context.FinancialPostingRuleMappings.Add(existing);
        }

        existing.Direction = input.Direction;
        existing.FinancialAccountId = input.FinancialAccountId;
        existing.Selector = selector;
        existing.UpdatedAtUtc = _clock.UtcNow;

        rule.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new FinancialPostingRuleMappingDto(
            existing.Id,
            rule.Id,
            rule.Code,
            existing.Sequence,
            existing.Direction,
            account.Id,
            account.Code,
            account.Name,
            existing.Selector);
    }

    public async Task DeletePostingRuleMappingAsync(
        string ruleCode,
        Guid mappingId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _context.FinancialPostingRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == ruleCode, cancellationToken)
            ?? throw new NotFoundException($"Posting rule '{ruleCode}' was not found.");

        var mapping = await _context.FinancialPostingRuleMappings
            .FirstOrDefaultAsync(m => m.Id == mappingId && m.RuleId == rule.Id, cancellationToken)
            ?? throw new NotFoundException($"Posting rule mapping '{mappingId}' was not found.");

        _context.FinancialPostingRuleMappings.Remove(mapping);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
