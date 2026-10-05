using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Finance;
using ClubStack.Contracts.Common;
using ClubStack.Domain.Finance;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Chart of accounts, club base currency and posting-rule mappings (D6 B2, B3,
/// B7, B17). Accounts are never destructively deleted; they are deactivated.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/accounts")]
[Authorize]
public sealed class FinancialAccountsController : AuditedControllerBase
{
    private readonly IFinancialAccountService _accounts;

    public FinancialAccountsController(
        IFinancialAccountService accounts,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _accounts = accounts;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountView)]
    [ProducesResponseType(typeof(FinancialAccountSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] FinancialAccountSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.SearchAccountsAsync(
            new FinancialAccountSearchInput(
                request.Code,
                request.Name,
                request.AccountType,
                request.Status,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(new FinancialAccountSearchResponse
        {
            Items = result.Items.Select(ToAccountResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("system")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountView)]
    [ProducesResponseType(typeof(List<FinancialAccountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SystemAccounts(CancellationToken cancellationToken)
    {
        var accounts = await _accounts.GetSystemAccountsAsync(cancellationToken);
        return Ok(accounts.Select(ToAccountResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountView)]
    [ProducesResponseType(typeof(FinancialAccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(ToAccountResponse(await _accounts.GetAccountAsync(id, cancellationToken)));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountManage)]
    [ProducesResponseType(typeof(FinancialAccountResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateFinancialAccountRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var account = await _accounts.CreateAccountAsync(
            new CreateFinancialAccountInput(
                request.Code,
                request.Name,
                request.AccountType,
                request.ParentAccountId,
                request.Description,
                request.IsPostingAllowed,
                request.CashKind,
                request.CurrencyCode),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Account.Create",
            "FinancialAccount",
            account.Id.ToString(),
            account.Code,
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = account.Id }, ToAccountResponse(account));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountManage)]
    [ProducesResponseType(typeof(FinancialAccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFinancialAccountRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var account = await _accounts.UpdateAccountAsync(
            id,
            new UpdateFinancialAccountInput(
                request.Name,
                request.ParentAccountId,
                request.Description,
                request.IsPostingAllowed,
                request.CashKind),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.Account.Update",
            "FinancialAccount",
            account.Id.ToString(),
            account.Code,
            cancellationToken: cancellationToken);

        return Ok(ToAccountResponse(account));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountManage)]
    [ProducesResponseType(typeof(FinancialAccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var account = await _accounts.DeactivateAccountAsync(id, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.Account.Deactivate",
            "FinancialAccount",
            account.Id.ToString(),
            account.Code,
            cancellationToken: cancellationToken);

        return Ok(ToAccountResponse(account));
    }

    [HttpGet("base-currency")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountView)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBaseCurrency(CancellationToken cancellationToken)
        => Ok(await _accounts.GetBaseCurrencyAsync(cancellationToken));

    [HttpPut("base-currency")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetBaseCurrency([FromBody] SetBaseCurrencyRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _accounts.SetBaseCurrencyAsync(request.CurrencyCode, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.BaseCurrency.Set",
            "SystemConfiguration",
            reference: request.CurrencyCode.ToUpperInvariant(),
            cancellationToken: cancellationToken);

        return NoContent();
    }

    [HttpGet("posting-rules")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountView)]
    [ProducesResponseType(typeof(List<FinancialPostingRuleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostingRules(CancellationToken cancellationToken)
    {
        var rules = await _accounts.GetPostingRulesAsync(cancellationToken);
        return Ok(rules.Select(ToRuleResponse).ToList());
    }

    [HttpPut("posting-rules/{ruleCode}/mappings")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountManage)]
    [ProducesResponseType(typeof(FinancialPostingRuleMappingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertMapping(
        string ruleCode,
        [FromBody] UpsertPostingRuleMappingRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = await _accounts.UpsertPostingRuleMappingAsync(
            ruleCode,
            new UpsertPostingRuleMappingInput(
                request.Sequence,
                request.Direction,
                request.FinancialAccountId,
                request.Selector),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Finance.PostingRule.Mapping.Upsert",
            "FinancialPostingRuleMapping",
            mapping.Id.ToString(),
            mapping.FinancialAccountCode,
            cancellationToken: cancellationToken);

        return Ok(ToMappingResponse(mapping));
    }

    [HttpDelete("posting-rules/{ruleCode}/mappings/{mappingId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceAccountManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMapping(string ruleCode, Guid mappingId, CancellationToken cancellationToken)
    {
        await _accounts.DeletePostingRuleMappingAsync(ruleCode, mappingId, ActorUserId(), cancellationToken);

        await RecordAuditAsync(
            "Finance.PostingRule.Mapping.Delete",
            "FinancialPostingRuleMapping",
            mappingId.ToString(),
            ruleCode,
            cancellationToken: cancellationToken);

        return NoContent();
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static FinancialAccountResponse ToAccountResponse(FinancialAccountDto a) => new()
    {
        Id = a.Id,
        Reference = a.Reference,
        Code = a.Code,
        Name = a.Name,
        AccountType = a.AccountType,
        NormalBalance = a.NormalBalance,
        Status = a.Status,
        ParentAccountId = a.ParentAccountId,
        Description = a.Description,
        IsSystem = a.IsSystem,
        CashKind = a.CashKind,
        IsPostingAllowed = a.IsPostingAllowed,
        CurrencyCode = a.CurrencyCode,
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
    };

    internal static FinancialPostingRuleMappingResponse ToMappingResponse(FinancialPostingRuleMappingDto m) => new()
    {
        Id = m.Id,
        RuleId = m.RuleId,
        RuleCode = m.RuleCode,
        Sequence = m.Sequence,
        Direction = m.Direction,
        FinancialAccountId = m.FinancialAccountId,
        FinancialAccountCode = m.FinancialAccountCode,
        FinancialAccountName = m.FinancialAccountName,
        Selector = m.Selector,
    };

    internal static FinancialPostingRuleResponse ToRuleResponse(FinancialPostingRuleDto r) => new()
    {
        Id = r.Id,
        Code = r.Code,
        Name = r.Name,
        SourceSection = r.SourceSection,
        Strategy = r.Strategy,
        Description = r.Description,
        Mappings = r.Mappings.Select(ToMappingResponse).ToList(),
    };
}
