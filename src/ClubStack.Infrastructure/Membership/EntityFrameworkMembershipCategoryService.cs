using ClubStack.Application.Exceptions;
using ClubStack.Application.Membership;
using ClubStack.Domain.Common;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Membership;

/// <inheritdoc />
public sealed class EntityFrameworkMembershipCategoryService : IMembershipCategoryService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkMembershipCategoryService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MembershipCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _context.MembershipCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        return categories.Select(MembershipCategoryMappings.ToDto).ToList();
    }

    public async Task<MembershipCategoryDto> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.MembershipCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Membership category '{id}' was not found.");
        return MembershipCategoryMappings.ToDto(category);
    }

    public async Task<MembershipCategoryDto> CreateCategoryAsync(MembershipCategoryInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Code))
        {
            throw new ValidationException(
                "The membership category could not be created.",
                MembershipServiceHelpers.FieldError("code", "A code is required."));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The membership category could not be created.",
                MembershipServiceHelpers.FieldError("name", "A name is required."));
        }

        if (input.RenewalPeriodMonths < 1)
        {
            throw new ValidationException(
                "The membership category could not be created.",
                MembershipServiceHelpers.FieldError("renewalPeriodMonths", "The renewal period must be at least 1 month."));
        }

        var code = input.Code.Trim();
        if (await _context.MembershipCategories.AnyAsync(c => c.Code == code, cancellationToken))
        {
            throw new ConflictException($"A membership category with code '{code}' already exists.");
        }

        var category = new MembershipCategoryEntity
        {
            Code = code,
            Name = input.Name.Trim(),
            Description = input.Description ?? string.Empty,
            EligibilityRules = input.EligibilityRules ?? string.Empty,
            RenewalPeriodMonths = input.RenewalPeriodMonths,
            PrivilegeConfiguration = input.PrivilegeConfiguration ?? string.Empty,
            GuestRules = input.GuestRules ?? string.Empty,
            AccountRules = input.AccountRules ?? string.Empty,
            CredentialRules = input.CredentialRules ?? string.Empty,
            IsActive = input.IsActive,
        };

        _context.MembershipCategories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return MembershipCategoryMappings.ToDto(category);
    }

    public async Task<MembershipCategoryDto> UpdateCategoryAsync(Guid id, MembershipCategoryInput input, CancellationToken cancellationToken = default)
    {
        var category = await _context.MembershipCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                       ?? throw new NotFoundException($"Membership category '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(input.Code))
        {
            throw new ValidationException(
                "The membership category could not be updated.",
                MembershipServiceHelpers.FieldError("code", "A code is required."));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The membership category could not be updated.",
                MembershipServiceHelpers.FieldError("name", "A name is required."));
        }

        if (input.RenewalPeriodMonths < 1)
        {
            throw new ValidationException(
                "The membership category could not be updated.",
                MembershipServiceHelpers.FieldError("renewalPeriodMonths", "The renewal period must be at least 1 month."));
        }

        var code = input.Code.Trim();
        if (await _context.MembershipCategories.AnyAsync(c => c.Code == code && c.Id != id, cancellationToken))
        {
            throw new ConflictException($"A membership category with code '{code}' already exists.");
        }

        category.Code = code;
        category.Name = input.Name.Trim();
        category.Description = input.Description ?? string.Empty;
        category.EligibilityRules = input.EligibilityRules ?? string.Empty;
        category.RenewalPeriodMonths = input.RenewalPeriodMonths;
        category.PrivilegeConfiguration = input.PrivilegeConfiguration ?? string.Empty;
        category.GuestRules = input.GuestRules ?? string.Empty;
        category.AccountRules = input.AccountRules ?? string.Empty;
        category.CredentialRules = input.CredentialRules ?? string.Empty;
        category.IsActive = input.IsActive;
        category.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MembershipCategoryMappings.ToDto(category);
    }

    public async Task<IReadOnlyList<MembershipFeeDto>> GetCategoryFeesAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryAsync(categoryId, cancellationToken);

        var fees = await _context.MembershipFees.AsNoTracking()
            .Where(f => f.CategoryId == categoryId)
            .OrderBy(f => f.FeeType)
            .ToListAsync(cancellationToken);

        return fees.Select(MembershipCategoryMappings.ToFeeDto).ToList();
    }

    public async Task<MembershipFeeDto> CreateFeeAsync(Guid categoryId, MembershipFeeInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryAsync(categoryId, cancellationToken);

        var feeType = MembershipServiceHelpers.ParseEnum<MembershipFeeType>(input.FeeType, "feeType");

        if (input.Amount <= 0)
        {
            throw new ValidationException(
                "The fee definition could not be created.",
                MembershipServiceHelpers.FieldError("amount", "The amount must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(input.CurrencyCode))
        {
            throw new ValidationException(
                "The fee definition could not be created.",
                MembershipServiceHelpers.FieldError("currencyCode", "A currency code is required."));
        }

        if (await _context.MembershipFees.AnyAsync(
                f => f.CategoryId == categoryId && f.FeeType == feeType, cancellationToken))
        {
            throw new ConflictException($"A fee of type '{feeType}' is already configured for this category.");
        }

        var fee = new MembershipFeeEntity
        {
            CategoryId = categoryId,
            FeeType = feeType,
            Description = input.Description ?? string.Empty,
            Amount = input.Amount,
            CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant(),
            IsProrationEnabled = input.IsProrationEnabled,
            IsActive = input.IsActive,
        };

        _context.MembershipFees.Add(fee);
        await _context.SaveChangesAsync(cancellationToken);

        return MembershipCategoryMappings.ToFeeDto(fee);
    }

    public async Task<MembershipFeeDto> UpdateFeeAsync(Guid categoryId, Guid feeId, MembershipFeeInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryAsync(categoryId, cancellationToken);

        var fee = await _context.MembershipFees
            .FirstOrDefaultAsync(f => f.Id == feeId && f.CategoryId == categoryId, cancellationToken)
            ?? throw new NotFoundException($"A fee definition with id '{feeId}' was not found for this category.");

        var feeType = MembershipServiceHelpers.ParseEnum<MembershipFeeType>(input.FeeType, "feeType");

        if (input.Amount <= 0)
        {
            throw new ValidationException(
                "The fee definition could not be updated.",
                MembershipServiceHelpers.FieldError("amount", "The amount must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(input.CurrencyCode))
        {
            throw new ValidationException(
                "The fee definition could not be updated.",
                MembershipServiceHelpers.FieldError("currencyCode", "A currency code is required."));
        }

        if (await _context.MembershipFees.AnyAsync(
                f => f.CategoryId == categoryId && f.FeeType == feeType && f.Id != feeId, cancellationToken))
        {
            throw new ConflictException($"A fee of type '{feeType}' is already configured for this category.");
        }

        fee.FeeType = feeType;
        fee.Description = input.Description ?? string.Empty;
        fee.Amount = input.Amount;
        fee.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        fee.IsProrationEnabled = input.IsProrationEnabled;
        fee.IsActive = input.IsActive;
        fee.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MembershipCategoryMappings.ToFeeDto(fee);
    }

    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await _context.MembershipCategories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new NotFoundException($"Membership category '{categoryId}' was not found.");
        }
    }
}

/// <summary>Category and fee-definition DTO mappings.</summary>
internal static class MembershipCategoryMappings
{
    public static MembershipCategoryDto ToDto(MembershipCategoryEntity c) => new(
        c.Id,
        c.Code,
        c.Name,
        c.Description,
        c.EligibilityRules,
        c.RenewalPeriodMonths,
        c.PrivilegeConfiguration,
        c.GuestRules,
        c.AccountRules,
        c.CredentialRules,
        c.IsActive,
        c.CreatedAtUtc,
        c.UpdatedAtUtc);

    public static MembershipFeeDto ToFeeDto(MembershipFeeEntity f) => new(
        f.Id,
        f.CategoryId,
        MembershipServiceHelpers.ToName(f.FeeType),
        f.Description,
        f.Amount,
        f.CurrencyCode,
        f.IsProrationEnabled,
        f.IsActive,
        f.UpdatedAtUtc);
}