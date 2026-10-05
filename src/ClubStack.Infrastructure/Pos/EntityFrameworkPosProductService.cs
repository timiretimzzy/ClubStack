using ClubStack.Application.Credentials;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Pos;
using ClubStack.Domain.Inventory;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Pos;

/// <summary>
/// POS catalog lookup (D5 POS). Returns active products with their current
/// selling price (member price when a member is in context, otherwise standard),
/// the available quantity at the register's inventory location and the product's
/// tax configuration snapshotted at sale time. Also resolves members by
/// membership number, name or credential for member identification.
/// </summary>
public sealed class EntityFrameworkPosProductService : IPosProductService
{
    private const string StandardPriceType = "Standard";
    private const string MemberPriceType = "Member";

    private readonly ClubStackDbContext _context;
    private readonly ICredentialService _credentials;

    public EntityFrameworkPosProductService(ClubStackDbContext context, ICredentialService credentials)
    {
        _context = context;
        _credentials = credentials;
    }

    public async Task<PosProductSearchResult> SearchAsync(PosProductSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = PosServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<ProductEntity> query = _context.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active);

        var queryText = PosServiceHelpers.NormalizeOptional(input.Query);
        if (queryText is not null)
        {
            query = query.Where(p =>
                p.Sku.ToLower().Contains(queryText.ToLower())
                || p.Name.ToLower().Contains(queryText.ToLower())
                || (p.Barcode != null && p.Barcode.ToLower().Contains(queryText.ToLower())));
        }

        if (input.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        var total = await query.CountAsync(cancellationToken);
        var products = await query
            .Include(p => p.TaxRate)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return new PosProductSearchResult(new List<PosProductDto>(), total, page, pageSize);
        }

        var productIds = products.Select(p => p.Id).ToList();
        var prices = await _context.ProductPrices
            .AsNoTracking()
            .Where(pr => productIds.Contains(pr.ProductId))
            .ToListAsync(cancellationToken);

        var balances = new Dictionary<(Guid, Guid), decimal>();
        if (input.LocationId is { } locationId && products.Any(p => p.StockManaged))
        {
            var managedIds = products.Where(p => p.StockManaged).Select(p => p.Id).ToList();
            balances = await _context.StockBalances
                .AsNoTracking()
                .Where(b => b.LocationId == locationId && managedIds.Contains(b.ProductId))
                .ToDictionaryAsync(b => (b.ProductId, b.LocationId), b => b.Quantity, cancellationToken);
        }

        var items = products.Select(p =>
        {
            var (amount, priceSource) = ResolveSellingPrice(p.Id, prices, input.MemberId);
            return new PosProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Barcode,
                p.UnitOfMeasure,
                p.StockManaged,
                amount,
                priceSource,
                IsOverride: false,
                p.StockManaged && input.LocationId is { } locationId ? balances.GetValueOrDefault((p.Id, locationId)) : null,
                p.CurrencyCode,
                p.TaxTreatment.ToString(),
                p.TaxRate?.Name,
                p.TaxRate?.Rate);
        }).ToList();

        return new PosProductSearchResult(items, total, page, pageSize);
    }

    public async Task<PosMemberSearchResult> SearchMembersAsync(PosMemberSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = PosServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<MemberEntity> query = _context.Members
            .AsNoTracking()
            .Include(m => m.Person)
            .Where(m => m.Status == ClubStack.Domain.Membership.MemberStatus.Active);

        var queryText = PosServiceHelpers.NormalizeOptional(input.Query);
        if (queryText is not null)
        {
            query = query.Where(m =>
                m.MemberNumber.ToLower().StartsWith(queryText.ToLower())
                || m.Person.LegalName.ToLower().Contains(queryText.ToLower())
                || m.Person.PreferredName.ToLower().Contains(queryText.ToLower()));
        }

        if (input.CredentialId is { } credentialId)
        {
            var memberId = await _context.Credentials
                .Where(c => c.Id == credentialId)
                .Select(c => (Guid?)c.MemberId)
                .FirstOrDefaultAsync(cancellationToken);
            if (memberId is null)
            {
                throw new NotFoundException($"Credential '{credentialId}' was not found.");
            }

            var resolvedMemberId = memberId.Value;
            query = query.Where(m => m.Id == resolvedMemberId);
        }

        var total = await query.CountAsync(cancellationToken);
        var members = await query
            .OrderBy(m => m.MemberNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var memberIds = members.Select(m => m.Id).ToList();
        var credentials = await _context.Credentials
            .AsNoTracking()
            .Where(c => c.MemberId != null && memberIds.Contains(c.MemberId.Value))
            .Where(c => c.Status == ClubStack.Domain.Credentials.CredentialStatus.Active)
            .ToListAsync(cancellationToken);

        var items = members.Select(m =>
        {
            var credential = credentials.FirstOrDefault(c => c.MemberId == m.Id);
            return new PosMemberDto(
                m.Id,
                m.MemberNumber,
                PosServiceHelpers.MemberDisplayName(m),
                m.Status.ToString(),
                credential?.Id,
                credential?.CredentialNumber,
                credential?.Status.ToString());
        }).ToList();

        return new PosMemberSearchResult(items, total, page, pageSize);
    }

    internal static (decimal Amount, string PriceSourceType) ResolveSellingPrice(Guid productId, IReadOnlyList<ProductPriceEntity> prices, Guid? memberId)
    {
        if (memberId is { } member && member != Guid.Empty)
        {
            var memberPrice = prices
                .Where(p => p.ProductId == productId && p.PriceType == MemberPriceType)
                .OrderByDescending(p => p.EffectiveFromUtc)
                .ThenByDescending(p => p.CreatedAtUtc)
                .Select(p => (decimal?)p.Amount)
                .FirstOrDefault();
            if (memberPrice is { } memberAmount)
            {
                return (memberAmount, MemberPriceType);
            }
        }

        var standard = prices
            .Where(p => p.ProductId == productId && p.PriceType == StandardPriceType)
            .OrderByDescending(p => p.EffectiveFromUtc)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => (decimal?)p.Amount)
            .FirstOrDefault();

        return (standard ?? 0m, StandardPriceType);
    }
}