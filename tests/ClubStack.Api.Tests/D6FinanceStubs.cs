using ClubStack.Application.Auditing;
using ClubStack.Application.Finance;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;

namespace ClubStack.Api.Tests;

/// <summary>
/// Stub finance services for <see cref="D6FinanceAuthorizationTests"/>.
///
/// Authorization is what those tests assert, so these answer every call with a
/// minimal valid DTO. Nothing here encodes accounting behaviour: that is proved
/// by the PostgreSQL integration suite, and a stub that threw would abort the
/// TestServer request instead of letting the test observe the authorization
/// outcome.
/// </summary>
internal abstract class FinanceStub
{
    protected static readonly Guid Id = new("11111111-1111-1111-1111-111111111111");

    protected static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class StubSystemClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class StubAuditRecorder : IAuditRecorder
{
    public Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AuditRecord>> GetRecentAsync(string? action, int take = 50, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

    public Task<AuditQueryResult> QueryAsync(AuditQueryInput query, CancellationToken cancellationToken = default)
        => Task.FromResult(new AuditQueryResult([], 0, query.Page, query.PageSize));
}

internal sealed class StubFinancialAccountService : FinanceStub, IFinancialAccountService
{
    private static readonly FinancialAccountDto Account = new(
        FinanceStub.Id, "FA-000001", "1000", "Cash on hand", FinancialAccountType.Asset, NormalBalance.Debit,
        FinancialAccountStatus.Active, null, "Stub", false, FinancialCashKind.CashOnHand, true, "ZAR", Now, Now);

    private static readonly FinancialPostingRuleMappingDto Mapping = new(
        FinanceStub.Id, FinanceStub.Id, "POS-CASH-SALE", 1, JournalDirection.Debit, FinanceStub.Id, "1000", "Cash on hand", null);

    public Task<FinancialAccountSearchResult> SearchAccountsAsync(FinancialAccountSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new FinancialAccountSearchResult([Account], 1, input.Page, input.PageSize));

    public Task<FinancialAccountDto> GetAccountAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Account);

    public Task<IReadOnlyList<FinancialAccountDto>> GetSystemAccountsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FinancialAccountDto>>([Account]);

    public Task<FinancialAccountDto> CreateAccountAsync(CreateFinancialAccountInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Account);

    public Task<FinancialAccountDto> UpdateAccountAsync(Guid id, UpdateFinancialAccountInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Account);

    public Task<FinancialAccountDto> DeactivateAccountAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Account);

    public Task<string> GetBaseCurrencyAsync(CancellationToken cancellationToken = default) => Task.FromResult("ZAR");

    public Task SetBaseCurrencyAsync(string currencyCode, string actorUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<FinancialPostingRuleDto>> GetPostingRulesAsync(CancellationToken cancellationToken = default)
    {
        var rule = new FinancialPostingRuleDto(
            FinanceStub.Id, "POS-CASH-SALE", "POS cash sale", "14.1", FinancialPostingStrategy.Direct, "Stub", [Mapping]);
        return Task.FromResult<IReadOnlyList<FinancialPostingRuleDto>>([rule]);
    }

    public Task<FinancialPostingRuleMappingDto> UpsertPostingRuleMappingAsync(string ruleCode, UpsertPostingRuleMappingInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Mapping);

    public Task DeletePostingRuleMappingAsync(string ruleCode, Guid mappingId, string actorUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class StubJournalService : FinanceStub, IJournalService
{
    private static readonly JournalDto Journal = new(
        FinanceStub.Id, "JRN-000001", JournalStatus.Draft, "Stub journal", FinanceStub.Id, "PER-000001",
        FinanceStub.Now, FinanceStub.Now, "ZAR", "ZAR", 1m, FinanceStub.Now, "MANUAL", FinanceStub.Id, null,
        null, null, null, null, null, 0m, 0m, null, null, null, "test-user", "corr", FinanceStub.Now, Array.Empty<JournalLineDto>());

    public Task<JournalSearchResult> SearchJournalsAsync(JournalSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new JournalSearchResult([Journal], 1, input.Page, input.PageSize));

    public Task<JournalDto> GetJournalAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Journal);

    public Task<JournalDto> GetJournalByReferenceAsync(string reference, CancellationToken cancellationToken = default)
        => Task.FromResult(Journal);

    public Task<JournalDto> CreateDraftAsync(CreateJournalInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Journal);

    public Task<JournalValidationResult> ValidateAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(new JournalValidationResult(true, [], 0m, 0m, 2));

    public Task<JournalDto> PostAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Journal);

    public Task<JournalDto> CancelAsync(Guid id, CancelJournalInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Journal);

    public Task<JournalDto> ReverseAsync(Guid id, ReverseJournalInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Journal);
}

internal sealed class StubFinancialPeriodService : FinanceStub, IFinancialPeriodService
{
    private static readonly FinancialPeriodDto Period = new(
        FinanceStub.Id, "PER-000001", 2026, 1, "January 2026", FinanceStub.Now,
        FinanceStub.Now.AddMonths(1).AddTicks(-1), FinancialPeriodStatus.Open, FinanceStub.Now, "test-user",
        null, null, null, null, null, 0, 0m, 0m);

    public Task<FinancialPeriodSearchResult> SearchPeriodsAsync(FinancialPeriodSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new FinancialPeriodSearchResult([Period], 1, input.Page, input.PageSize));

    public Task<FinancialPeriodDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Period);

    public Task<FinancialPeriodDto?> ResolveForTransactionDateAsync(DateTimeOffset transactionDate, CancellationToken cancellationToken = default)
        => Task.FromResult<FinancialPeriodDto?>(Period);

    public Task<FinancialPeriodDto> OpenAsync(OpenFinancialPeriodInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Period);

    public Task<FinancialPeriodDto> CloseAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Period);

    public Task<FinancialPeriodDto> ReopenAsync(Guid id, ReopenFinancialPeriodInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Period);
}

internal sealed class StubReceivableService : FinanceStub, IReceivableService
{
    private static readonly ReceivableDto Receivable = new(
        FinanceStub.Id, "AR-000001", FinanceStub.Id, "M-0001", "Test Member", null, "POS-SALE", FinanceStub.Id,
        null, 100m, 0m, 100m, FinanceStub.Now, null, "ZAR", ReceivableStatus.Open, FinanceStub.Id, "JRN-000001",
        "Stub", string.Empty, FinanceStub.Now, []);

    public Task<ReceivableSearchResult> SearchAsync(ReceivableSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new ReceivableSearchResult([Receivable], 1, 100m, input.Page, input.PageSize));

    public Task<ReceivableDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Receivable);

    public Task<ReceivableDto?> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<ReceivableDto?>(Receivable);

    public Task<ReceivableDto> CreateAsync(CreateReceivableInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Receivable);

    public Task<ReceivableDto> CancelAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Receivable);
}

internal sealed class StubPayableService : FinanceStub, IPayableService
{
    private static readonly PayableDto Payable = new(
        FinanceStub.Id, "AP-000001", FinanceStub.Id, "SUP-001", "Test Supplier", "SUPPLIER-INVOICE", FinanceStub.Id,
        null, 100m, 0m, 100m, FinanceStub.Now, null, "ZAR", PayableStatus.Open, FinanceStub.Id, "JRN-000001",
        "Stub", string.Empty, FinanceStub.Now, []);

    public Task<PayableSearchResult> SearchAsync(PayableSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new PayableSearchResult([Payable], 1, 100m, input.Page, input.PageSize));

    public Task<PayableDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Payable);

    public Task<PayableDto?> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<PayableDto?>(Payable);

    public Task<PayableDto> CreateAsync(CreatePayableInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Payable);

    public Task<PayableDto> CancelAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Payable);
}

internal sealed class StubFinancePaymentService : FinanceStub, IFinancePaymentService
{
    private static readonly PaymentDto Payment = new(
        FinanceStub.Id, "PAY-000001", PaymentDirection.Received, FinanceStub.Id, "M-0001", null, null, null, null,
        "POS-SALE", FinanceStub.Id, null, 100m, 100m, "ZAR", FinanceStub.Now, null, "CASH", null, FinanceStub.Id,
        "JRN-000001", "Stub", null, "test-user", FinanceStub.Now, Array.Empty<PaymentAllocationDto>());

    public Task<PaymentSearchResult> SearchAsync(PaymentSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentSearchResult([Payment], 1, input.Page, input.PageSize));

    public Task<PaymentDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Payment);

    public Task<PaymentDto?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentDto?>(null);

    public Task<PaymentDto> RecordAsync(RecordPaymentInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Payment);
}

internal sealed class StubReconciliationService : FinanceStub, IFinancialReconciliationService
{
    private static readonly FinancialReconciliationDto Reconciliation = new(
        FinanceStub.Id, "REC-000001", FinanceStub.Id, "1000", "Cash on hand", FinanceStub.Now, 100m, 100m, 0m,
        FinancialReconciliationStatus.InProgress, "Stub", "test-user", FinanceStub.Now, null, null, null, [], 0m);

    public Task<FinancialReconciliationSearchResult> SearchAsync(FinancialReconciliationSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new FinancialReconciliationSearchResult([Reconciliation], 1, input.Page, input.PageSize));

    public Task<FinancialReconciliationDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Reconciliation);

    public Task<FinancialReconciliationDto> CreateAsync(CreateReconciliationInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Reconciliation);

    public Task<FinancialReconciliationDto> AddMatchAsync(Guid id, MatchReconciliationLineInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Reconciliation);

    public Task<FinancialReconciliationDto> RemoveMatchAsync(Guid id, Guid matchId, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Reconciliation);

    public Task<FinancialReconciliationDto> CompleteAsync(Guid id, CompleteReconciliationInput input, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Reconciliation);

    public Task<FinancialReconciliationDto> CancelAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Reconciliation);
}

internal sealed class StubReportService : FinanceStub, IFinanceReportService
{
    public Task<TrialBalanceReportDto> GetTrialBalanceAsync(DateTimeOffset? asOf = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new TrialBalanceReportDto("ZAR", asOf, [], 0m, 0m, 0m));

    public Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(Guid financialAccountId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new GeneralLedgerReportDto(financialAccountId, "1000", "Cash on hand", "ZAR", from, to, 0m, 0m, []));

    public Task<AgingReportDto> GetReceivableAgingAsync(DateTimeOffset? asOf = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new AgingReportDto(true, "ZAR", asOf ?? FinanceStub.Now, [], [], 0m));

    public Task<AgingReportDto> GetPayableAgingAsync(DateTimeOffset? asOf = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new AgingReportDto(false, "ZAR", asOf ?? FinanceStub.Now, [], [], 0m));

    public Task<CashPositionReportDto> GetCashPositionAsync(DateTimeOffset? asOf = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new CashPositionReportDto("ZAR", asOf ?? FinanceStub.Now, [], 0m, 0m));

    public Task<FinancialPeriodsReportDto> GetFinancialPeriodsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new FinancialPeriodsReportDto([], null, null));

    public Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new FinanceOverviewDto("ZAR", FinanceStub.Now, null, 0m, 0m, 0m, 0m, 0m, 0m, 0, 0));
}
