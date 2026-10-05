using System.Security.Claims;
using ClubStack.Api.Contracts;
using ClubStack.Application.Accounts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Controllers;

/// <summary>
/// Member account ledger (Phase 2 ACC-001..014, Phase 3 §17-20, Phase 5 §33/§34).
/// Balances are always derived from posted transactions; payments, credits,
/// adjustments and reversals are audited commands on the append-only ledger.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/member-accounts")]
[Authorize]
public sealed class MemberAccountsController : AuditedControllerBase
{
    private readonly IMemberAccountService _accounts;

    public MemberAccountsController(
        IMemberAccountService accounts,
        IAuditRecorder auditRecorder,
        ICorrelationContext correlation,
        ISystemClock clock)
        : base(auditRecorder, correlation, clock)
    {
        _accounts = accounts;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsAccountView)]
    [ProducesResponseType(typeof(MemberAccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAccountAsync(id, cancellationToken);
        return Ok(ToAccountResponse(account));
    }

    [HttpGet("{id:guid}/balance")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsAccountView)]
    [ProducesResponseType(typeof(AccountBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Balance(Guid id, CancellationToken cancellationToken)
    {
        var balance = await _accounts.GetBalanceAsync(id, cancellationToken);
        return Ok(ToBalanceResponse(balance));
    }

    [HttpGet("{id:guid}/transactions")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsTransactionView)]
    [ProducesResponseType(typeof(AccountTransactionSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transactions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _accounts.SearchTransactionsAsync(new AccountTransactionSearchInput(id, page, pageSize), cancellationToken);
        return Ok(new AccountTransactionSearchResponse
        {
            Items = result.Items.Select(ToTransactionResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpPost("{id:guid}/charges")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsTransactionCreate)]
    [ProducesResponseType(typeof(AccountTransactionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Charge(Guid id, [FromBody] ChargeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transaction = await _accounts.ChargeAsync(
            id,
            new ChargeInput(request.Description, request.Amount, request.CurrencyCode ?? string.Empty, request.Reference, request.SourceType, request.SourceId, request.BusinessDateUtc),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Accounts.Transaction.Charge",
            "AccountTransaction",
            transaction.Id.ToString(),
            transaction.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToTransactionResponse(transaction));
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsPaymentRecord)]
    [ProducesResponseType(typeof(AccountTransactionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] PaymentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transaction = await _accounts.RecordPaymentAsync(
            id,
            new PaymentInput(
                request.Description,
                request.Amount,
                request.CurrencyCode ?? string.Empty,
                request.PaymentMethodCode,
                request.ExternalReference,
                request.BusinessDateUtc,
                request.Allocations?.Select(a => new PaymentAllocationInput(a.ChargeTransactionId, a.Amount)).ToList()),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Accounts.Payment.Record",
            "AccountTransaction",
            transaction.Id.ToString(),
            transaction.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToTransactionResponse(transaction));
    }

    [HttpPost("{id:guid}/credits")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsCreditCreate)]
    [ProducesResponseType(typeof(AccountTransactionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCredit(Guid id, [FromBody] CreditRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transaction = await _accounts.CreateCreditAsync(
            id,
            new CreditInput(
                request.Description,
                request.Amount,
                request.CurrencyCode ?? string.Empty,
                request.ExternalReference,
                request.BusinessDateUtc,
                request.Allocations?.Select(a => new PaymentAllocationInput(a.ChargeTransactionId, a.Amount)).ToList()),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Accounts.Credit.Create",
            "AccountTransaction",
            transaction.Id.ToString(),
            transaction.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToTransactionResponse(transaction));
    }

    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsAdjustmentCreate)]
    [ProducesResponseType(typeof(AccountTransactionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] AdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transaction = await _accounts.AdjustAsync(
            id,
            new AdjustmentInput(request.Description, request.Amount, request.Reason, request.BusinessDateUtc),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Accounts.Transaction.Adjust",
            "AccountTransaction",
            transaction.Id.ToString(),
            transaction.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToTransactionResponse(transaction));
    }

    [HttpPost("{id:guid}/transactions/{transactionId:guid}/reverse")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsPaymentReverse)]
    [ProducesResponseType(typeof(AccountTransactionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reverse(Guid id, Guid transactionId, [FromBody] ReversalRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _accounts.ReverseAsync(
            id,
            transactionId,
            new ReversalInput(request.Reason),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Accounts.Payment.Reverse",
            "AccountTransaction",
            transaction.Id.ToString(),
            transaction.Reference,
            cancellationToken: cancellationToken);

        return Ok(ToTransactionResponse(transaction));
    }

    [HttpPost("{id:guid}/statement")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsStatementGenerate)]
    [ProducesResponseType(typeof(StatementResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateStatement(Guid id, [FromBody] StatementRequest? request, CancellationToken cancellationToken)
    {
        var statement = await _accounts.GenerateStatementAsync(
            id,
            new StatementPeriodInput(request?.FromDateUtc, request?.ToDateUtc),
            ActorUserId(),
            cancellationToken);

        await RecordAuditAsync(
            "Accounts.Statement.Generate",
            "AccountStatement",
            statement.AccountId.ToString(),
            statement.StatementNumber,
            cancellationToken: cancellationToken);

        return Ok(ToStatementResponse(statement));
    }

    [HttpGet("{id:guid}/aging")]
    [Authorize(Policy = "Permission:" + Permissions.AccountsAgingView)]
    [ProducesResponseType(typeof(AgingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Aging(Guid id, CancellationToken cancellationToken)
    {
        var aging = await _accounts.GetAgingAsync(id, cancellationToken);
        return Ok(new AgingResponse
        {
            AsOfDateUtc = aging.AsOfDateUtc,
            CurrencyCode = aging.CurrencyCode,
            DaysOutstandingThreshold = aging.DaysOutstandingThreshold,
            TotalOutstanding = aging.TotalOutstanding,
            OpenItemCount = aging.OpenItemCount,
            Buckets = aging.Buckets
                .Select(b => new AgingBucketResponse { Label = b.Label, DaysFrom = b.DaysFrom, DaysTo = b.DaysTo, OutstandingAmount = b.OutstandingAmount })
                .ToList(),
            Items = aging.Items
                .Select(i => new AgingItemResponse
                {
                    ChargeTransactionId = i.ChargeTransactionId,
                    Description = i.Description,
                    BusinessDateUtc = i.BusinessDateUtc,
                    DaysOutstanding = i.DaysOutstanding,
                    OutstandingAmount = i.OutstandingAmount,
                })
                .ToList(),
        });
    }

    private string ActorUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    internal static AccountBalanceResponse ToBalanceResponse(AccountBalanceDto b) => new()
    {
        CurrencyCode = b.CurrencyCode,
        AmountDue = b.AmountDue,
        CreditsBalance = b.CreditsBalance,
        UnallocatedPayments = b.UnallocatedPayments,
        OverdueBalance = b.OverdueBalance,
        AsOfDateUtc = b.AsOfDateUtc,
    };

    internal static MemberAccountResponse ToAccountResponse(MemberAccountDto a) => new()
    {
        Id = a.Id,
        MemberId = a.MemberId,
        MemberNumber = a.MemberNumber,
        MemberName = a.MemberName,
        AccountNumber = a.AccountNumber,
        Status = a.Status,
        CurrencyCode = a.CurrencyCode,
        OpenedAtUtc = a.OpenedAtUtc,
        ClosedAtUtc = a.ClosedAtUtc,
        Balance = ToBalanceResponse(a.Balance),
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
    };

    internal static AccountTransactionResponse ToTransactionResponse(AccountTransactionDto t) => new()
    {
        Id = t.Id,
        AccountId = t.AccountId,
        TransactionType = t.TransactionType,
        Status = t.Status,
        CurrencyCode = t.CurrencyCode,
        Amount = t.Amount,
        SignedAmount = t.SignedAmount,
        IsDebit = t.IsDebit,
        Description = t.Description,
        Reference = t.Reference,
        ExternalReference = t.ExternalReference,
        Reason = t.Reason,
        SourceType = t.SourceType,
        SourceId = t.SourceId,
        ReverseOfTransactionId = t.ReverseOfTransactionId,
        PaymentMethodId = t.PaymentMethodId,
        PaymentMethodCode = t.PaymentMethodCode,
        PaymentState = t.PaymentState,
        TransactionDateUtc = t.TransactionDateUtc,
        BusinessDateUtc = t.BusinessDateUtc,
        CreatedAtUtc = t.CreatedAtUtc,
    };

    internal static StatementResponse ToStatementResponse(StatementDto s) => new()
    {
        AccountId = s.AccountId,
        AccountNumber = s.AccountNumber,
        MemberNumber = s.MemberNumber,
        MemberName = s.MemberName,
        CurrencyCode = s.CurrencyCode,
        StatementNumber = s.StatementNumber,
        FromDateUtc = s.FromDateUtc,
        ToDateUtc = s.ToDateUtc,
        OpeningBalance = s.OpeningBalance,
        ClosingBalance = s.ClosingBalance,
        CurrentBalance = s.CurrentBalance,
        GeneratedAtUtc = s.GeneratedAtUtc,
        GeneratedByUserId = s.GeneratedByUserId,
        Transactions = s.Transactions.Select(ToTransactionResponse).ToList(),
    };
}