using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Contracts.Common;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Pos;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// POS catalog and configuration (D5 POS). Product search feeds the register
/// screen; member identification resolves a member from a convenience credential
/// (key tag / barcode) at the till.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/pos")]
[Authorize]
public sealed class PosController : AuditedControllerBase
{
    private readonly IPosProductService _products;
    private readonly IPosConfigurationService _configuration;

    public PosController(
        IPosProductService products,
        IPosConfigurationService configuration,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _products = products;
        _configuration = configuration;
    }

    [HttpGet("products")]
    [Authorize(Policy = "Permission:" + Permissions.PosProductView)]
    [ProducesResponseType(typeof(PosProductSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] string? query,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? memberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.SearchAsync(
            new PosProductSearchInput(query, categoryId, locationId, memberId, page, pageSize),
            cancellationToken);

        return Ok(new PosProductSearchResponse
        {
            Items = result.Items.Select(ToProductResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("products/members")]
    [Authorize(Policy = "PermissionAny:PosMemberLookup")]
    [ProducesResponseType(typeof(PosMemberSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMembers(
        [FromQuery] string? query,
        [FromQuery] Guid? credentialId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.SearchMembersAsync(
            new PosMemberSearchInput(query, credentialId, page, pageSize),
            cancellationToken);

        return Ok(new PosMemberSearchResponse
        {
            Items = result.Items.Select(ToMemberResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("configuration")]
    [Authorize(Policy = "Permission:" + Permissions.PosRegisterView)]
    [ProducesResponseType(typeof(PosConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfiguration(CancellationToken cancellationToken)
    {
        var state = await _configuration.GetStateAsync(cancellationToken);
        return Ok(new PosConfigurationResponse
        {
            Outlets = state.Outlets.Select(o => new PosOutletResponse
            {
                Id = o.Id,
                Code = o.Code,
                Name = o.Name,
                Notes = o.Notes,
                Status = o.Status,
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc,
            }).ToList(),
            Registers = state.Registers.Select(ToRegisterResponse).ToList(),
        });
    }

    [HttpPost("outlets")]
    [Authorize(Policy = "Permission:" + Permissions.PosRegisterManage)]
    [ProducesResponseType(typeof(PosOutletResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOutlet([FromBody] CreatePosOutletRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var outlet = await _configuration.CreateOutletAsync(
            new CreatePosOutletInput(request.Code, request.Name, request.Notes),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync("POS.Outlet.Create", "PosOutlet", outlet.Id.ToString(), outlet.Code, cancellationToken: cancellationToken);

        return Ok(new PosOutletResponse
        {
            Id = outlet.Id,
            Code = outlet.Code,
            Name = outlet.Name,
            Notes = outlet.Notes,
            Status = outlet.Status,
            CreatedAtUtc = outlet.CreatedAtUtc,
            UpdatedAtUtc = outlet.UpdatedAtUtc,
        });
    }

    [HttpPost("registers")]
    [Authorize(Policy = "Permission:" + Permissions.PosRegisterManage)]
    [ProducesResponseType(typeof(PosRegisterResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRegister([FromBody] CreatePosRegisterRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var register = await _configuration.CreateRegisterAsync(
            new CreatePosRegisterInput(request.PosOutletId, request.Code, request.Name, request.InventoryLocationId),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync("POS.Register.Create", "PosRegister", register.Id.ToString(), register.Code, cancellationToken: cancellationToken);

        return Ok(ToRegisterResponse(register));
    }

    private static PosProductResponse ToProductResponse(PosProductDto p) => new()
    {
        Id = p.Id,
        Sku = p.Sku,
        Name = p.Name,
        Barcode = p.Barcode,
        UnitOfMeasure = p.UnitOfMeasure,
        StockManaged = p.StockManaged,
        UnitPrice = p.UnitPrice,
        PriceSourceType = p.PriceSourceType,
        IsOverride = p.IsOverride,
        AvailableQuantity = p.AvailableQuantity,
        CurrencyCode = p.CurrencyCode,
        TaxTreatment = p.TaxTreatment,
        TaxRateName = p.TaxRateName,
        TaxRate = p.TaxRate,
    };

    private static PosMemberResponse ToMemberResponse(PosMemberDto m) => new()
    {
        MemberId = m.MemberId,
        MemberNumber = m.MemberNumber,
        MemberName = m.MemberName,
        Status = m.Status,
        CredentialId = m.CredentialId,
        CredentialNumber = m.CredentialNumber,
        CredentialStatus = m.CredentialStatus,
    };

    private static PosRegisterResponse ToRegisterResponse(PosRegisterDto r) => new()
    {
        Id = r.Id,
        PosOutletId = r.PosOutletId,
        PosOutletCode = r.PosOutletCode,
        PosOutletName = r.PosOutletName,
        Code = r.Code,
        Name = r.Name,
        InventoryLocationId = r.InventoryLocationId,
        InventoryLocationCode = r.InventoryLocationCode,
        InventoryLocationName = r.InventoryLocationName,
        Status = r.Status,
        CurrentSessionId = r.CurrentSessionId,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc,
    };

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}