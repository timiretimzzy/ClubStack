using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Membership;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Membership category configuration and per-category fee definitions
/// (Phase 2 MEM-021/MEM-022/MEM-040/MEM-043/MEM-044).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/membership-categories")]
[Authorize]
public sealed class MembershipCategoriesController : AuditedControllerBase
{
    private readonly IMembershipCategoryService _categories;

    public MembershipCategoriesController(
        IMembershipCategoryService categories,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _categories = categories;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryView)]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var categories = await _categories.GetCategoriesAsync(cancellationToken);
        return Ok(categories.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryView)]
    [ProducesResponseType(typeof(MembershipCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categories.GetCategoryAsync(id, cancellationToken);
        return Ok(ToResponse(category));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryManage)]
    [ProducesResponseType(typeof(MembershipCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] MembershipCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var category = await _categories.CreateCategoryAsync(ToInput(request), cancellationToken);

        await RecordAuditAsync(
            "Membership.Category.Create",
            "MembershipCategory",
            category.Id.ToString(),
            category.Code,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(category));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryManage)]
    [ProducesResponseType(typeof(MembershipCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] MembershipCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var category = await _categories.UpdateCategoryAsync(id, ToInput(request), cancellationToken);

        await RecordAuditAsync(
            "Membership.Category.Update",
            "MembershipCategory",
            category.Id.ToString(),
            category.Code,
            cancellationToken: cancellationToken);

        return Ok(ToResponse(category));
    }

    [HttpGet("{id:guid}/fees")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryView)]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipFeeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFees(Guid id, CancellationToken cancellationToken)
    {
        var fees = await _categories.GetCategoryFeesAsync(id, cancellationToken);
        return Ok(fees.Select(ToFeeResponse).ToList());
    }

    [HttpPost("{id:guid}/fees")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryManage)]
    [ProducesResponseType(typeof(MembershipFeeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFee(Guid id, [FromBody] MembershipFeeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var fee = await _categories.CreateFeeAsync(id, ToFeeInput(request), cancellationToken);

        await RecordAuditAsync(
            "Membership.CategoryFee.Create",
            "MembershipFee",
            fee.Id.ToString(),
            fee.FeeType,
            cancellationToken: cancellationToken);

        return Ok(ToFeeResponse(fee));
    }

    [HttpPut("{id:guid}/fees/{feeId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.MembershipCategoryManage)]
    [ProducesResponseType(typeof(MembershipFeeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFee(Guid id, Guid feeId, [FromBody] MembershipFeeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var fee = await _categories.UpdateFeeAsync(id, feeId, ToFeeInput(request), cancellationToken);

        await RecordAuditAsync(
            "Membership.CategoryFee.Update",
            "MembershipFee",
            fee.Id.ToString(),
            fee.FeeType,
            cancellationToken: cancellationToken);

        return Ok(ToFeeResponse(fee));
    }

    internal static MembershipCategoryInput ToInput(MembershipCategoryRequest r) => new(
        r.Code,
        r.Name,
        r.Description,
        r.EligibilityRules,
        r.RenewalPeriodMonths,
        r.PrivilegeConfiguration,
        r.GuestRules,
        r.AccountRules,
        r.CredentialRules,
        r.IsActive);

    private static MembershipFeeInput ToFeeInput(MembershipFeeRequest r) => new(
        r.FeeType,
        r.Description,
        r.Amount,
        r.CurrencyCode,
        r.IsProrationEnabled,
        r.IsActive);

    private static MembershipCategoryResponse ToResponse(MembershipCategoryDto c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        EligibilityRules = c.EligibilityRules,
        RenewalPeriodMonths = c.RenewalPeriodMonths,
        PrivilegeConfiguration = c.PrivilegeConfiguration,
        GuestRules = c.GuestRules,
        AccountRules = c.AccountRules,
        CredentialRules = c.CredentialRules,
        IsActive = c.IsActive,
        CreatedAtUtc = c.CreatedAtUtc,
        UpdatedAtUtc = c.UpdatedAtUtc,
    };

    private static MembershipFeeResponse ToFeeResponse(MembershipFeeDto f) => new()
    {
        Id = f.Id,
        CategoryId = f.CategoryId,
        FeeType = f.FeeType,
        Description = f.Description,
        Amount = f.Amount,
        CurrencyCode = f.CurrencyCode,
        IsProrationEnabled = f.IsProrationEnabled,
        IsActive = f.IsActive,
        UpdatedAtUtc = f.UpdatedAtUtc,
    };
}