using ClubStack.Application.Finance;
using ClubStack.Domain.Common;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Finance;

/// <summary>
/// Seeds the D6 posting-rule catalogue. The eight rule identities are the authoritative
/// Phase 8 §14.1-§14.8 rules (B7) and nothing else is added. Financial accounts and the
/// rule-to-account mappings are deliberately NOT seeded: they are the club's accounting
/// configuration, configured through the finance screens, which is also why no account
/// identifier is ever hard-coded in a posting service.
/// </summary>
public sealed class FinanceReferenceSeeder
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public FinanceReferenceSeeder(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _context.FinancialPostingRules
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);

        var added = 0;
        var now = _clock.UtcNow;

        foreach (var rule in FinancialPostingRules.All)
        {
            if (existing.ContainsKey(rule.Code))
            {
                continue;
            }

            _context.FinancialPostingRules.Add(new FinancialPostingRuleEntity
            {
                Id = Guid.CreateVersion7(),
                Code = rule.Code,
                Name = rule.Name,
                SourceSection = rule.SourceSection,
                PostingStrategy = rule.Strategy,
                Description = rule.Description,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });

            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
