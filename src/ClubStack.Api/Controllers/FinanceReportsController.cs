using ClubStack.Api.Contracts;
using ClubStack.Application.Authorization;
using ClubStack.Application.Finance;
using ClubStack.Contracts.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubStack.Api.Controllers;

/// <summary>
/// The six mandated finance reports plus the finance overview (D6 B16). Every
/// figure is derived from posted journals; nothing is stored.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/finance/reports")]
[Authorize]
public sealed class FinanceReportsController : ControllerBase
{
    private readonly IFinanceReportService _reports;

    public FinanceReportsController(IFinanceReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("overview")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(FinanceOverviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var overview = await _reports.GetOverviewAsync(cancellationToken);

        return Ok(new FinanceOverviewResponse
        {
            BaseCurrencyCode = overview.BaseCurrencyCode,
            AsOf = overview.AsOf,
            CurrentPeriod = overview.CurrentPeriod is null
                ? null
                : FinancialPeriodsController.ToPeriodResponse(overview.CurrentPeriod),
            OpenReceivables = overview.OpenReceivables,
            OpenPayables = overview.OpenPayables,
            CashAndBank = overview.CashAndBank,
            TrialBalanceDebits = overview.TrialBalanceDebits,
            TrialBalanceCredits = overview.TrialBalanceCredits,
            TrialBalanceDifference = overview.TrialBalanceDifference,
            DraftJournalCount = overview.DraftJournalCount,
            UnreconciledCashAccountCount = overview.UnreconciledCashAccountCount,
        });
    }

    [HttpGet("trial-balance")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(TrialBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrialBalance([FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken)
    {
        var report = await _reports.GetTrialBalanceAsync(asOf, cancellationToken);

        return Ok(new TrialBalanceResponse
        {
            CurrencyCode = report.CurrencyCode,
            AsOf = report.AsOf,
            TotalDebit = report.TotalDebit,
            TotalCredit = report.TotalCredit,
            Difference = report.Difference,
            Rows = report.Rows.Select(r => new TrialBalanceRowResponse
            {
                AccountCode = r.AccountCode,
                AccountName = r.AccountName,
                AccountType = r.AccountType,
                NormalBalance = r.NormalBalance,
                Debit = r.Debit,
                Credit = r.Credit,
                Balance = r.Balance,
            }).ToList(),
        });
    }

    [HttpGet("general-ledger/{financialAccountId:guid}")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(GeneralLedgerResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GeneralLedger(
        Guid financialAccountId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var report = await _reports.GetGeneralLedgerAsync(financialAccountId, from, to, cancellationToken);

        return Ok(new GeneralLedgerResponse
        {
            FinancialAccountId = report.FinancialAccountId,
            AccountCode = report.AccountCode,
            AccountName = report.AccountName,
            CurrencyCode = report.CurrencyCode,
            From = report.From,
            To = report.To,
            OpeningBalance = report.OpeningBalance,
            ClosingBalance = report.ClosingBalance,
            Entries = report.Entries.Select(e => new GeneralLedgerRowResponse
            {
                TransactionDate = e.TransactionDate,
                JournalReference = e.JournalReference,
                JournalDescription = e.JournalDescription,
                AccountCode = e.AccountCode,
                AccountName = e.AccountName,
                Direction = e.Direction,
                Amount = e.Amount,
                LineDescription = e.LineDescription,
                SourceReference = e.SourceReference,
            }).ToList(),
        });
    }

    [HttpGet("receivable-aging")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(FinanceAgingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivableAging([FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken)
        => Ok(ToAgingResponse(await _reports.GetReceivableAgingAsync(asOf, cancellationToken)));

    [HttpGet("payable-aging")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(FinanceAgingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayableAging([FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken)
        => Ok(ToAgingResponse(await _reports.GetPayableAgingAsync(asOf, cancellationToken)));

    [HttpGet("cash-position")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(CashPositionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CashPosition([FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken)
    {
        var report = await _reports.GetCashPositionAsync(asOf, cancellationToken);

        return Ok(new CashPositionResponse
        {
            BaseCurrencyCode = report.BaseCurrencyCode,
            AsOf = report.AsOf,
            TotalCash = report.TotalCash,
            TotalBank = report.TotalBank,
            Rows = report.Rows.Select(r => new CashPositionRowResponse
            {
                AccountCode = r.AccountCode,
                AccountName = r.AccountName,
                CashKind = r.CashKind,
                DebitBalance = r.DebitBalance,
                CreditBalance = r.CreditBalance,
                NetBalance = r.NetBalance,
                CurrencyCode = r.CurrencyCode,
            }).ToList(),
        });
    }

    [HttpGet("periods")]
    [Authorize(Policy = "Permission:" + Permissions.FinanceReportView)]
    [ProducesResponseType(typeof(FinancialPeriodsReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Periods(CancellationToken cancellationToken)
    {
        var report = await _reports.GetFinancialPeriodsAsync(cancellationToken);

        return Ok(new FinancialPeriodsReportResponse
        {
            CurrentStatus = report.CurrentStatus,
            CurrentPeriod = report.CurrentPeriod is null
                ? null
                : FinancialPeriodsController.ToPeriodResponse(report.CurrentPeriod),
            Rows = report.Rows.Select(r => new FinancialPeriodsReportRowResponse
            {
                Reference = r.Reference,
                Year = r.Year,
                Month = r.Month,
                Name = r.Name,
                Status = r.Status,
                StartUtc = r.StartUtc,
                EndUtc = r.EndUtc,
                PostedDebitTotal = r.PostedDebitTotal,
                PostedCreditTotal = r.PostedCreditTotal,
                PostedJournalCount = r.PostedJournalCount,
            }).ToList(),
        });
    }

    private static FinanceAgingResponse ToAgingResponse(AgingReportDto a) => new()
    {
        IsReceivable = a.IsReceivable,
        CurrencyCode = a.CurrencyCode,
        AsOf = a.AsOf,
        TotalOutstanding = a.TotalOutstanding,
        Buckets = a.Buckets.Select(b => new FinanceAgingBucketResponse
        {
            Label = b.Label,
            DaysFrom = b.DaysFrom,
            DaysTo = b.DaysTo,
            OutstandingAmount = b.OutstandingAmount,
            DocumentCount = b.DocumentCount,
        }).ToList(),
        Documents = a.Documents.Select(d => new FinanceAgingDocumentResponse
        {
            DocumentId = d.DocumentId,
            Reference = d.Reference,
            CounterpartyCode = d.CounterpartyCode,
            CounterpartyName = d.CounterpartyName,
            TransactionDate = d.TransactionDate,
            DueDate = d.DueDate,
            DaysOutstanding = d.DaysOutstanding,
            BucketLabel = d.BucketLabel,
            OriginalAmount = d.OriginalAmount,
            AllocatedAmount = d.AllocatedAmount,
            OutstandingAmount = d.OutstandingAmount,
            CurrencyCode = d.CurrencyCode,
        }).ToList(),
    };
}
