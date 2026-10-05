using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Domain.Finance;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D6 finance integrity against real PostgreSQL. These tests exist because most of the
/// B1-B17 guarantees are database-level, not application-level: the D6 migration installs
/// the balance, immutability, open-period and duplicate-source rules as triggers and
/// unique indexes, and this suite proves they actually fire.
/// </summary>
[Collection("Database")]
public sealed class D6FinanceIntegrityTests : DatabaseTestBase
{
    public D6FinanceIntegrityTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // ---------------------------------------------------------------------
    // B3 / B4: balance and immutability
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Database_rejects_an_unbalanced_posted_journal()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var journal = new JournalEntity
        {
            Reference = $"JRN-{RandomReference()}",
            Status = JournalStatus.Posted,
            Description = "Deliberately unbalanced",
            FinancialPeriodId = period.Id,
            TransactionDate = period.StartUtc.AddDays(1),
            BusinessDate = period.StartUtc.AddDays(1),
            TransactionCurrencyCode = "ZAR",
            BaseCurrencyCode = "ZAR",
            ExchangeRate = 1m,
            RateDateUtc = period.StartUtc.AddDays(1),
            SourceType = "D6Test",
            SourceId = Guid.CreateVersion7(),
            PostedAtUtc = DateTimeOffset.UtcNow,
            PostedByUserId = "user-1",
            CreatedByUserId = "user-1",
            CorrelationId = "corr-1",
        };
        journal.Lines.Add(NewLine(journal.Id, 1, chart.Receivable, JournalDirection.Debit, 100m));
        journal.Lines.Add(NewLine(journal.Id, 2, chart.Revenue, JournalDirection.Credit, 90m));
        context.Journals.Add(journal);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        Assert.Contains("not balanced", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Database_rejects_a_posted_journal_with_a_single_line()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var journal = NewPostedJournal(period, "Single line");
        journal.Lines.Add(NewLine(journal.Id, 1, chart.Receivable, JournalDirection.Debit, 100m));
        journal.Lines.Add(NewLine(journal.Id, 2, chart.Revenue, JournalDirection.Credit, 100m));
        context.Journals.Add(journal);
        await context.SaveChangesAsync();

        // Cancelling then removing a line is a draft-only operation, so a posted journal
        // must refuse the delete outright.
        context.JournalLines.RemoveRange(journal.Lines);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        Assert.Contains("immutable", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Posted_journal_cannot_be_mutated()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();
        var journal = await PostBalancedJournalAsync(chart, period, "Mutation attempt");

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var tracked = await context.Journals.SingleAsync(j => j.Id == journal.Id);
        tracked.Description = "Rewritten history";
        tracked.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        Assert.Contains("cannot be changed", Flatten(ex), StringComparison.OrdinalIgnoreCase);

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal("Mutation attempt", (await verify.Journals.SingleAsync(j => j.Id == journal.Id)).Description);
    }

    [Fact]
    public async Task Lines_cannot_be_added_to_a_posted_journal_in_a_later_transaction()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();
        var journal = await PostBalancedJournalAsync(chart, period, "Late line");

        // A separate DbContext means a separate transaction, which is exactly the case the
        // PostingTransactionId stamp exists to reject.
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        context.JournalLines.Add(NewLine(journal.Id, 3, chart.Receivable, JournalDirection.Debit, 10m));
        context.JournalLines.Add(NewLine(journal.Id, 4, chart.Revenue, JournalDirection.Credit, 10m));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        Assert.Contains("cannot be added later", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Posting_engine_writes_a_balanced_journal_in_a_single_transaction()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        using var scope = Services.CreateScope();
        var posting = scope.ServiceProvider.GetRequiredService<IFinancePostingService>();
        var sourceId = Guid.CreateVersion7();

        var journal = await posting.PostAsync(
            Request("POS-CASH-SALE", sourceId, "Sale 1", 100m, period),
            "user-1",
            "corr-post");

        Assert.Equal(JournalStatus.Posted, journal.Status);
        Assert.True(journal.Lines.Count >= 2);
        Assert.Equal(
            journal.Lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.Amount),
            journal.Lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.Amount));

        // The stamp is set by the database only on the transition to Posted, and the
        // journal_lines trigger refuses any line written from a different transaction, so
        // the journal and its lines provably share one transaction. It cannot be compared
        // with txid_current() here because that is a new connection with a new transaction.
        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            // The column is deliberately absent from the EF model, so read it as raw SQL.
            var connection = context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT "PostingTransactionId" IS NOT NULL
                FROM "journals" WHERE "Id" = @id
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = journal.Id;
            command.Parameters.Add(parameter);

            Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }

        var originalLineCount = journal.Lines.Count;
        var tampered = false;
        try
        {
            await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
            context.JournalLines.Add(new JournalLineEntity
            {
                Id = Guid.CreateVersion7(),
                JournalId = journal.Id,
                FinancialAccountId = chart.Revenue,
                LineNumber = originalLineCount + 1,
                Direction = JournalDirection.Credit,
                Amount = 100m,
                BaseAmount = 100m,
                Description = "Injected from a separate transaction",
            });
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            tampered = true;
        }

        Assert.True(tampered, "A posted journal accepted a line written outside its posting transaction.");
    }

    // ---------------------------------------------------------------------
    // B6: duplicate source
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Reposting_the_same_source_returns_the_original_journal()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();
        var sourceId = Guid.CreateVersion7();

        using var scope = Services.CreateScope();
        var posting = scope.ServiceProvider.GetRequiredService<IFinancePostingService>();

        var first = await posting.PostAsync(Request("POS-CASH-SALE", sourceId, "Idempotent", 100m, period), "user-1", "c1");
        var replay = await posting.PostAsync(Request("POS-CASH-SALE", sourceId, "Idempotent", 100m, period), "user-1", "c2");

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.Reference, replay.Reference);

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await verify.Journals.CountAsync(j => j.SourceType == "POS-CASH-SALE" && j.SourceId == sourceId));
    }

    [Fact]
    public async Task Database_rejects_two_receivables_for_one_source()
    {
        var memberId = await CreateMemberAsync();
        var chart = await CreateChartAsync();
        var sourceId = Guid.CreateVersion7();

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.AccountsReceivable.Add(NewReceivable(memberId, "D6Test", sourceId));
            await context.SaveChangesAsync();
        }

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            context.AccountsReceivable.Add(NewReceivable(memberId, "D6Test", sourceId));
            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        }
    }

    // ---------------------------------------------------------------------
    // B5: periods
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Database_rejects_posting_into_a_closed_period()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        // B5: a period with no posted journals may not be closed, so post one first.
        await PostBalancedJournalAsync(chart, period, "Before close");

        using (var scope = Services.CreateScope())
        {
            var periods = scope.ServiceProvider.GetRequiredService<IFinancialPeriodService>();
            var closed = await periods.CloseAsync(period.Id, "user-1");
            Assert.Equal(FinancialPeriodStatus.Closed, closed.Status);
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var journal = NewPostedJournal(period, "Closed period");
        journal.Lines.Add(NewLine(journal.Id, 1, chart.Receivable, JournalDirection.Debit, 100m));
        journal.Lines.Add(NewLine(journal.Id, 2, chart.Revenue, JournalDirection.Credit, 100m));
        context.Journals.Add(journal);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        Assert.Contains("closed", Flatten(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Period_lifecycle_is_open_close_reopen_with_audit()
    {
        var chart = await CreateChartAsync();
        var (year, month) = NextPeriodParts();
        var name = UniquePrefix("PER");

        using var scope = Services.CreateScope();
        var periods = scope.ServiceProvider.GetRequiredService<IFinancialPeriodService>();

        var opened = await periods.OpenAsync(new OpenFinancialPeriodInput(year, month, name), "user-1");
        Assert.Equal(FinancialPeriodStatus.Open, opened.Status);
        Assert.StartsWith("PER-", opened.Reference, StringComparison.Ordinal);

        // B5: an empty period may not be closed, so a posted journal is required.
        await PostBalancedJournalAsync(chart, opened, "Period activity");

        var closed = await periods.CloseAsync(opened.Id, "user-1");
        Assert.Equal(FinancialPeriodStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAtUtc);
        Assert.Equal(1, closed.PostedJournalCount);

        var reopened = await periods.ReopenAsync(opened.Id, new ReopenFinancialPeriodInput("D6 test reopen"), "user-1");
        Assert.Equal(FinancialPeriodStatus.Open, reopened.Status);
        Assert.Equal("D6 test reopen", reopened.ReopenReason);
        Assert.NotNull(reopened.ReopenedAtUtc);
        Assert.Equal("user-1", reopened.ReopenedByUserId);
    }

    // ---------------------------------------------------------------------
    // B13 / B14: receivable and payable lifecycles
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Receivable_lifecycle_moves_open_to_partially_paid_to_paid()
    {
        var memberId = await CreateMemberAsync();
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        Guid receivableId;
        using (var scope = Services.CreateScope())
        {
            var receivables = scope.ServiceProvider.GetRequiredService<IReceivableService>();
            var created = await receivables.CreateAsync(
                new CreateReceivableInput(
                    memberId,
                    "D6Test",
                    Guid.CreateVersion7(),
                    "Invoice 1",
                    300m,
                    period.StartUtc.AddDays(1),
                    period.StartUtc.AddDays(30),
                    "ZAR",
                    null,
                    null,
                    "Membership dues",
                    FinancialPostingRuleCodes.PosMemberAccountCharge),
                "user-1");

            Assert.Equal(ReceivableStatus.Open, created.Status);
            Assert.Equal(300m, created.OutstandingAmount);
            Assert.Equal(0m, created.AllocatedAmount);
            receivableId = created.Id;
        }

        // Part payment.
        using (var scope = Services.CreateScope())
        {
            var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
            var first = await payments.RecordAsync(
                new RecordPaymentInput(
                    PaymentDirection.Received, memberId, null, null, null,
                    "D6Test", Guid.CreateVersion7(), "Receipt 1",
                    120m, "ZAR", period.StartUtc.AddDays(2), null, "Cash", null, "Part payment",
                    [new PaymentAllocationInput(receivableId, null, 120m)],
                    FinancialPostingRuleCodes.MemberAccountPayment,
                    IdempotencyKey: null),
                "user-1");

            Assert.Equal(PaymentDirection.Received, first.Direction);
            Assert.Equal(0m, first.UnappliedAmount);
        }

        await using (var verify = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var row = await verify.AccountsReceivable.AsNoTracking().SingleAsync(r => r.Id == receivableId);
            Assert.Equal(ReceivableStatus.PartiallyPaid, row.Status);
            Assert.Equal(120m, row.AllocatedAmount);
            Assert.Equal(180m, row.OutstandingAmount);
        }

        // Settle the remainder, partly unapplied to prove unapplied tracking.
        using (var scope = Services.CreateScope())
        {
            var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
            var second = await payments.RecordAsync(
                new RecordPaymentInput(
                    PaymentDirection.Received, memberId, null, null, null,
                    "D6Test", Guid.CreateVersion7(), "Receipt 2",
                    200m, "ZAR", period.StartUtc.AddDays(3), null, "Cash", null, "Final payment",
                    [new PaymentAllocationInput(receivableId, null, 180m)],
                    FinancialPostingRuleCodes.MemberAccountPayment,
                    IdempotencyKey: null),
                "user-1");

            Assert.Equal(20m, second.UnappliedAmount);
            Assert.Single(second.Allocations);
        }

        await using (var verify = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var row = await verify.AccountsReceivable.AsNoTracking().SingleAsync(r => r.Id == receivableId);
            Assert.Equal(ReceivableStatus.Paid, row.Status);
            Assert.Equal(0m, row.OutstandingAmount);
        }
    }

    [Fact]
    public async Task Payment_replay_with_the_same_idempotency_key_does_not_double_post()
    {
        var memberId = await CreateMemberAsync();
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();
        var key = UniquePrefix("IDEM");

        using (var scope = Services.CreateScope())
        {
            var receivables = scope.ServiceProvider.GetRequiredService<IReceivableService>();
            await receivables.CreateAsync(
                new CreateReceivableInput(
                    memberId, "D6Test", Guid.CreateVersion7(), "Invoice", 100m,
                    period.StartUtc.AddDays(1), null, "ZAR", null, null, "Dues",
                    FinancialPostingRuleCodes.PosMemberAccountCharge),
                "user-1");
        }

        Guid receivableId;
        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            receivableId = await context.AccountsReceivable
                .Where(r => r.SourceType == "D6Test")
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(r => r.Id)
                .FirstAsync();
        }

        PaymentDto first;
        using (var scope = Services.CreateScope())
        {
            var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
            first = await payments.RecordAsync(
                new RecordPaymentInput(
                    PaymentDirection.Received, memberId, null, null, null,
                    "D6Test", Guid.CreateVersion7(), "Receipt", 100m, "ZAR",
                    period.StartUtc.AddDays(2), null, "Cash", null, "Payment",
                    [new PaymentAllocationInput(receivableId, null, 100m)],
                    FinancialPostingRuleCodes.MemberAccountPayment, key),
                "user-1");
        }

        PaymentDto replay;
        using (var scope = Services.CreateScope())
        {
            var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
            replay = await payments.RecordAsync(
                new RecordPaymentInput(
                    PaymentDirection.Received, memberId, null, null, null,
                    "D6Test", Guid.CreateVersion7(), "Receipt", 100m, "ZAR",
                    period.StartUtc.AddDays(2), null, "Cash", null, "Payment",
                    [new PaymentAllocationInput(receivableId, null, 100m)],
                    FinancialPostingRuleCodes.MemberAccountPayment, key),
                "user-1");
        }

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.Reference, replay.Reference);

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await verify.Payments.CountAsync(p => p.IdempotencyKey == key));
    }

    [Fact]
    public async Task Over_allocating_a_receivable_is_rejected()
    {
        var memberId = await CreateMemberAsync();
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        Guid receivableId;
        using (var scope = Services.CreateScope())
        {
            var receivables = scope.ServiceProvider.GetRequiredService<IReceivableService>();
            var created = await receivables.CreateAsync(
                new CreateReceivableInput(
                    memberId, "D6Test", Guid.CreateVersion7(), "Invoice", 100m,
                    period.StartUtc.AddDays(1), null, "ZAR", null, null, "Dues",
                    FinancialPostingRuleCodes.PosMemberAccountCharge),
                "user-1");
            receivableId = created.Id;

            var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
            await Assert.ThrowsAsync<ValidationException>(() => payments.RecordAsync(
                new RecordPaymentInput(
                    PaymentDirection.Received, memberId, null, null, null,
                    "D6Test", Guid.CreateVersion7(), "Receipt", 150m, "ZAR",
                    period.StartUtc.AddDays(2), null, "Cash", null, "Too much",
                    [new PaymentAllocationInput(receivableId, null, 150m)],
                    FinancialPostingRuleCodes.MemberAccountPayment, null),
                "user-1"));
        }

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await verify.AccountsReceivable.AsNoTracking().SingleAsync(r => r.Id == receivableId);
        Assert.Equal(100m, row.OutstandingAmount);
        Assert.Equal(ReceivableStatus.Open, row.Status);
    }

    [Fact]
    public async Task Payable_lifecycle_allocates_against_a_supplier()
    {
        var supplierId = await CreateSupplierAsync();
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        Guid payableId;
        using (var scope = Services.CreateScope())
        {
            var payables = scope.ServiceProvider.GetRequiredService<IPayableService>();
            var created = await payables.CreateAsync(
                new CreatePayableInput(
                    supplierId, "D6Test", Guid.CreateVersion7(), "Bill 1", 500m,
                    period.StartUtc.AddDays(1), period.StartUtc.AddDays(14), "ZAR", null, null,
                    "Stock order", FinancialPostingRuleCodes.SupplierInvoice),
                "user-1");

            Assert.Equal(PayableStatus.Open, created.Status);
            payableId = created.Id;
        }

        using (var scope = Services.CreateScope())
        {
            var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
            var payment = await payments.RecordAsync(
                new RecordPaymentInput(
                    PaymentDirection.Made, null, supplierId, null, null,
                    "D6Test", Guid.CreateVersion7(), "EFT 1", 500m, "ZAR",
                    period.StartUtc.AddDays(5), null, "EFT", null, "Paid in full",
                    [new PaymentAllocationInput(null, payableId, 500m)],
                    FinancialPostingRuleCodes.SupplierPayment, null),
                "user-1");

            Assert.Equal(0m, payment.UnappliedAmount);
        }

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await verify.AccountsPayable.AsNoTracking().SingleAsync(p => p.Id == payableId);
        Assert.Equal(PayableStatus.Paid, row.Status);
        Assert.Equal(0m, row.OutstandingAmount);
    }

    // ---------------------------------------------------------------------
    // B4: reversal
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Reversal_creates_a_compensating_journal_and_links_the_original()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        using var scope = Services.CreateScope();
        var journals = scope.ServiceProvider.GetRequiredService<IJournalService>();

        var original = await journals.CreateDraftAsync(
            new CreateJournalInput(
                "Reversible entry", period.StartUtc.AddDays(1), null, "ZAR", null, null, period.Id,
                [
                    new CreateJournalLineInput(chart.Receivable, JournalDirection.Debit, 100m, "Debit"),
                    new CreateJournalLineInput(chart.Revenue, JournalDirection.Credit, 100m, "Credit"),
                ],
                "SRC-1"),
            "user-1");

        var posted = await journals.PostAsync(original.Id, "user-1");

        // The reversal needs its own open period. This period is far in the future so
        // tests never share it, and ReverseAsync defaults to today, so the date is
        // stated explicitly rather than relying on another test's current period.
        var reversed = await journals.ReverseAsync(
            posted.Id,
            new ReverseJournalInput("D6 test reversal", period.StartUtc.AddDays(2)),
            "user-1");

        Assert.Equal(JournalStatus.Posted, reversed.Status);
        Assert.Equal(posted.Id, reversed.ReversalOfJournalId);
        Assert.True(reversed.Lines.All(l => l.SourceLineId is not null));

        // Debits and credits are swapped relative to the original.
        var reversedDebit = reversed.Lines.Where(l => l.Direction == JournalDirection.Debit).Sum(l => l.Amount);
        var reversedCredit = reversed.Lines.Where(l => l.Direction == JournalDirection.Credit).Sum(l => l.Amount);
        Assert.Equal(reversedDebit, reversedCredit);
        Assert.Equal(100m, reversedDebit);

        // The database refuses a second reversal of the same journal.
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var duplicate = NewPostedJournal(period, "Second reversal", reversalOf: posted.Id);
        duplicate.Lines.Add(NewLine(duplicate.Id, 1, chart.Revenue, JournalDirection.Debit, 100m));
        duplicate.Lines.Add(NewLine(duplicate.Id, 2, chart.Receivable, JournalDirection.Credit, 100m));
        context.Journals.Add(duplicate);
        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
    }

    // ---------------------------------------------------------------------
    // B15 / B16: reconciliation and reports
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Reconciliation_completes_only_when_variance_is_zero()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        using var scope = Services.CreateScope();
        var journals = scope.ServiceProvider.GetRequiredService<IJournalService>();
        var reconciliations = scope.ServiceProvider.GetRequiredService<IFinancialReconciliationService>();

        var journal = await journals.CreateDraftAsync(
            new CreateJournalInput(
                "Cash receipt", period.StartUtc.AddDays(1), null, "ZAR", null, null, period.Id,
                [
                    new CreateJournalLineInput(chart.Cash, JournalDirection.Debit, 250m, "Cash in"),
                    new CreateJournalLineInput(chart.Revenue, JournalDirection.Credit, 250m, "Revenue"),
                ],
                "SRC-CASH"),
            "user-1");
        var posted = await journals.PostAsync(journal.Id, "user-1");

        // A wrong statement balance leaves a variance and cannot complete.
        var wrong = await reconciliations.CreateAsync(
            new CreateReconciliationInput(chart.Cash, period.StartUtc.AddDays(10), 900m, "Wrong figure"),
            "user-1");
        await reconciliations.AddMatchAsync(wrong.Id, new MatchReconciliationLineInput(posted.Id, 250m, null), "user-1");

        var completedWrong = await reconciliations.CompleteAsync(wrong.Id, new CompleteReconciliationInput(null), "user-1");
        Assert.NotEqual(0m, completedWrong.Variance);

        // The correct statement balance matches.
        var right = await reconciliations.CreateAsync(
            new CreateReconciliationInput(chart.Cash, period.StartUtc.AddDays(10), 250m, "Correct figure"),
            "user-1");
        await reconciliations.AddMatchAsync(right.Id, new MatchReconciliationLineInput(posted.Id, 250m, null), "user-1");

        var completedRight = await reconciliations.CompleteAsync(right.Id, new CompleteReconciliationInput("Balanced"), "user-1");
        Assert.Equal(FinancialReconciliationStatus.Completed, completedRight.Status);
        Assert.Equal(0m, completedRight.Variance);
    }

    [Fact]
    public async Task Cash_position_uses_the_configured_cash_classification()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        using (var scope = Services.CreateScope())
        {
            var journals = scope.ServiceProvider.GetRequiredService<IJournalService>();
            var draft = await journals.CreateDraftAsync(
                new CreateJournalInput(
                    "Bank receipt", period.StartUtc.AddDays(1), null, "ZAR", null, null, period.Id,
                    [
                        new CreateJournalLineInput(chart.Bank, JournalDirection.Debit, 700m, "Bank in"),
                        new CreateJournalLineInput(chart.Revenue, JournalDirection.Credit, 700m, "Revenue"),
                    ],
                    "SRC-BANK"),
                "user-1");
            await journals.PostAsync(draft.Id, "user-1");
        }

        using var scope2 = Services.CreateScope();
        var reports = scope2.ServiceProvider.GetRequiredService<IFinanceReportService>();

        // The report defaults asOf to now, and this period is deliberately in the far
        // future so tests never share it, so the asOf has to be stated explicitly.
        var asOf = period.EndUtc.AddDays(1);
        var cash = await reports.GetCashPositionAsync(asOf);

        // The shared fixture holds bank accounts from other tests, so scope by code.
        var bankCode = await AccountCodeAsync(chart.Bank);
        var bank = Assert.Single(cash.Rows, r => r.AccountCode == bankCode);
        Assert.Equal(FinancialCashKind.Bank, bank.CashKind);
        Assert.Equal(700m, bank.DebitBalance);

        var cashCode = await AccountCodeAsync(chart.Cash);
        Assert.Equal(0m, cash.Rows.Single(r => r.AccountCode == cashCode).DebitBalance);

        // A receivable account is not a cash account and must not appear.
        Assert.All(cash.Rows, r => Assert.NotEqual(FinancialCashKind.None, r.CashKind));
    }

    [Fact]
    public async Task Trial_balance_balances_for_a_posted_journal()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();
        var chartRevenueCode = await AccountCodeAsync(chart.Revenue);

        using (var scope = Services.CreateScope())
        {
            var journals = scope.ServiceProvider.GetRequiredService<IJournalService>();
            var draft = await journals.CreateDraftAsync(
                new CreateJournalInput(
                    "Balanced trial", period.StartUtc.AddDays(1), null, "ZAR", null, null, period.Id,
                    [
                        new CreateJournalLineInput(chart.Receivable, JournalDirection.Debit, 425.50m, "Dr"),
                        new CreateJournalLineInput(chart.Revenue, JournalDirection.Credit, 425.50m, "Cr"),
                    ],
                    "SRC-TB"),
                "user-1");
            await journals.PostAsync(draft.Id, "user-1");
        }

        using var scope2 = Services.CreateScope();
        var reports = scope2.ServiceProvider.GetRequiredService<IFinanceReportService>();
        var trial = await reports.GetTrialBalanceAsync();

        Assert.Equal(trial.TotalDebit, trial.TotalCredit);
        Assert.Equal(0m, trial.Difference);
        Assert.Contains(trial.Rows, r => r.AccountCode == chartRevenueCode);
    }

    // ---------------------------------------------------------------------
    // B7: reference formats
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Database_rejects_a_malformed_business_reference()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var journal = NewPostedJournal(period, "Bad reference");
        journal.Reference = "NOT-A-FINANCE-REFERENCE";
        journal.Lines.Add(NewLine(journal.Id, 1, chart.Receivable, JournalDirection.Debit, 10m));
        journal.Lines.Add(NewLine(journal.Id, 2, chart.Revenue, JournalDirection.Credit, 10m));
        context.Journals.Add(journal);

        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Every_finance_service_assigns_a_well_formed_reference()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();
        var memberId = await CreateMemberAsync();
        var supplierId = await CreateSupplierAsync();

        using var scope = Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountService>();
        var receivables = scope.ServiceProvider.GetRequiredService<IReceivableService>();
        var payables = scope.ServiceProvider.GetRequiredService<IPayableService>();
        var payments = scope.ServiceProvider.GetRequiredService<IFinancePaymentService>();
        var reconciliations = scope.ServiceProvider.GetRequiredService<IFinancialReconciliationService>();

        Assert.Matches("^FA-[0-9]{6}$", await AccountReferenceAsync(chart.Cash));
        Assert.Matches("^PER-[0-9]{6}$", period.Reference);

        var receivable = await receivables.CreateAsync(
            new CreateReceivableInput(memberId, "D6Test", Guid.CreateVersion7(), "Ref", 50m,
                period.StartUtc.AddDays(1), null, "ZAR", null, null, "Dues",
                FinancialPostingRuleCodes.PosMemberAccountCharge),
            "user-1");
        Assert.Matches("^AR-[0-9]{6}$", receivable.Reference);

        var payable = await payables.CreateAsync(
            new CreatePayableInput(supplierId, "D6Test", Guid.CreateVersion7(), "Ref", 50m,
                period.StartUtc.AddDays(1), null, "ZAR", null, null, "Bill",
                FinancialPostingRuleCodes.SupplierInvoice),
            "user-1");
        Assert.Matches("^AP-[0-9]{6}$", payable.Reference);

        var payment = await payments.RecordAsync(
            new RecordPaymentInput(PaymentDirection.Received, memberId, null, null, null,
                "D6Test", Guid.CreateVersion7(), "Ref", 50m, "ZAR",
                period.StartUtc.AddDays(2), null, "Cash", null, "Payment",
                [new PaymentAllocationInput(receivable.Id, null, 50m)],
                FinancialPostingRuleCodes.MemberAccountPayment, null),
            "user-1");
        Assert.Matches("^PAY-[0-9]{6}$", payment.Reference);

        var reconciliation = await reconciliations.CreateAsync(
            new CreateReconciliationInput(chart.Bank, period.StartUtc.AddDays(5), 0m, null),
            "user-1");
        Assert.Matches("^REC-[0-9]{6}$", reconciliation.Reference);

        Assert.NotNull(await accounts.GetBaseCurrencyAsync());
    }

    [Fact]
    public async Task Account_search_and_deactivation_preserve_history()
    {
        var chart = await CreateChartAsync();
        var period = await OpenCurrentPeriodAsync();

        using (var scope = Services.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountService>();
            var deactivated = await accounts.DeactivateAccountAsync(chart.Revenue, "user-1");
            Assert.Equal(FinancialAccountStatus.Inactive, deactivated.Status);

            var search = await accounts.SearchAccountsAsync(
                new FinancialAccountSearchInput(null, null, null, FinancialAccountStatus.Inactive));
            Assert.Contains(search.Items, a => a.Id == chart.Revenue);
        }

        // Historical journals keep a valid reference to the deactivated account.
        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(
            FinancialAccountStatus.Inactive,
            (await verify.FinancialAccounts.AsNoTracking().SingleAsync(a => a.Id == chart.Revenue)).Status);
        Assert.Equal(
            period.Id,
            (await verify.FinancialPeriods.AsNoTracking().SingleAsync(p => p.Id == period.Id)).Id);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private sealed record Chart(Guid Cash, Guid Bank, Guid Receivable, Guid Payable, Guid Revenue, Guid Expense);

    private static (int Year, int Month) CurrentPeriodParts()
    {
        var now = DateTimeOffset.UtcNow;
        return (now.Year, now.Month);
    }

    /// <summary>
    /// (Year, Month) is globally unique, and several tests close or dirty their period,
    /// so every test allocates its own far-future period instead of sharing the current
    /// one. The counter is static because xUnit reuses the class across the collection.
    /// </summary>
    private static int _periodSequence = -1;

    private static (int Year, int Month) NextPeriodParts()
    {
        var index = Interlocked.Increment(ref _periodSequence);
        return (2200 + (index / 12), (index % 12) + 1);
    }

    private static string RandomReference() => Random.Shared.Next(100000, 999999).ToString();

    private static string Flatten(Exception exception)
    {
        var text = exception.ToString();
        var inner = exception.InnerException;
        while (inner is not null)
        {
            text += " | " + inner.Message;
            inner = inner.InnerException;
        }

        return text;
    }

    private static JournalStatus ParseJournalStatus(string value) =>
        Enum.Parse<JournalStatus>(value, ignoreCase: true);

    private static JournalDirection ParseLineDirection(string value) =>
        Enum.Parse<JournalDirection>(value, ignoreCase: true);

    private static JournalLineEntity NewLine(
        Guid journalId,
        int number,
        Guid accountId,
        JournalDirection direction,
        decimal amount) => new()
        {
            JournalId = journalId,
            LineNumber = number,
            FinancialAccountId = accountId,
            Direction = direction,
            Amount = amount,
            CurrencyCode = "ZAR",
            BaseAmount = amount,
        };

    private static JournalEntity NewPostedJournal(
        FinancialPeriodDto period,
        string description,
        Guid? reversalOf = null)
    {
        var journal = new JournalEntity
        {
            Reference = $"JRN-{RandomReference()}",
            Status = JournalStatus.Posted,
            Description = description,
            FinancialPeriodId = period.Id,
            TransactionDate = period.StartUtc.AddDays(1),
            BusinessDate = period.StartUtc.AddDays(1),
            TransactionCurrencyCode = "ZAR",
            BaseCurrencyCode = "ZAR",
            ExchangeRate = 1m,
            RateDateUtc = period.StartUtc.AddDays(1),
            SourceType = "D6Test",
            SourceId = Guid.CreateVersion7(),
            PostedAtUtc = DateTimeOffset.UtcNow,
            PostedByUserId = "user-1",
            CreatedByUserId = "user-1",
            CorrelationId = "corr-1",
        };
        journal.ReversalOfJournalId = reversalOf;
        return journal;
    }

    private static AccountsReceivableEntity NewReceivable(Guid memberId, string sourceType, Guid sourceId) => new()
        {
            Reference = $"AR-{RandomReference()}",
            MemberId = memberId,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = "D6 test",
            OriginalAmount = 100m,
            AllocatedAmount = 0m,
            OutstandingAmount = 100m,
            TransactionDate = DateTimeOffset.UtcNow,
            CurrencyCode = "ZAR",
            Status = ReceivableStatus.Open,
            Description = "D6 test",
            CreatedByUserId = "user-1",
        };

    private FinancialPostingRequest Request(
        string ruleCode,
        Guid sourceId,
        string description,
        decimal amount,
        FinancialPeriodDto period) => new(
        ruleCode,
        ruleCode,
        sourceId,
        description,
        period.StartUtc.AddDays(1),
        period.StartUtc.AddDays(1),
        "ZAR",
        null,
        null,
        description,
        [new FinancialPostingAmount(null, amount)]);

    private async Task<JournalDto> PostBalancedJournalAsync(Chart chart, FinancialPeriodDto period, string description)
    {
        using var scope = Services.CreateScope();
        var journals = scope.ServiceProvider.GetRequiredService<IJournalService>();
        var draft = await journals.CreateDraftAsync(
            new CreateJournalInput(
                description, period.StartUtc.AddDays(1), null, "ZAR", null, null, period.Id,
                [
                    new CreateJournalLineInput(chart.Receivable, JournalDirection.Debit, 100m, "Debit"),
                    new CreateJournalLineInput(chart.Revenue, JournalDirection.Credit, 100m, "Credit"),
                ],
                "D6-TEST"),
            "user-1");

        return await journals.PostAsync(draft.Id, "user-1");
    }

    private async Task<Chart> CreateChartAsync()
    {
        await EnsureStarterCurrencyAsync();
        await EnsurePostingRulesAsync();

        using var scope = Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountService>();
        var tag = UniquePrefix("ACC");
        var counter = 0;

        async Task<Guid> Create(
            string suffix,
            string name,
            FinancialAccountType type,
            NormalBalance balance,
            FinancialCashKind cashKind)
        {
            counter++;
            var account = await accounts.CreateAccountAsync(
                new CreateFinancialAccountInput(
                    $"{tag}{counter}{suffix}",
                    name,
                    type,
                    null,
                    $"{name} ({tag})",
                    true,
                    cashKind,
                    "ZAR"),
                "user-1");
            return account.Id;
        }

        var chart = new Chart(
            await Create("C", "Cash on hand", FinancialAccountType.Asset, NormalBalance.Debit, FinancialCashKind.CashOnHand),
            await Create("B", "Bank account", FinancialAccountType.Asset, NormalBalance.Debit, FinancialCashKind.Bank),
            await Create("R", "Member receivable", FinancialAccountType.Asset, NormalBalance.Debit, FinancialCashKind.None),
            await Create("P", "Accounts payable", FinancialAccountType.Liability, NormalBalance.Credit, FinancialCashKind.None),
            await Create("V", "Revenue", FinancialAccountType.Revenue, NormalBalance.Credit, FinancialCashKind.None),
            await Create("E", "Expense", FinancialAccountType.Expense, NormalBalance.Debit, FinancialCashKind.None));

        await ConfigureMappingsAsync(chart);
        return chart;
    }

    /// <summary>
    /// The posting engine deliberately ships with no mappings: accounts and rule
    /// mappings are club configuration. These tests therefore configure the rules they
    /// exercise, which also proves the mapping-driven path end to end.
    /// </summary>
    private async Task ConfigureMappingsAsync(Chart chart)
    {
        using var scope = Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountService>();

        async Task Map(string rule, params (int Sequence, JournalDirection Direction, Guid Account)[] lines)
        {
            foreach (var (sequence, direction, account) in lines)
            {
                await accounts.UpsertPostingRuleMappingAsync(
                    rule,
                    new UpsertPostingRuleMappingInput(sequence, direction, account, null),
                    "user-1");
            }
        }

        await Map(FinancialPostingRuleCodes.PosCashSale,
            (1, JournalDirection.Debit, chart.Cash),
            (2, JournalDirection.Credit, chart.Revenue));
        await Map(FinancialPostingRuleCodes.PosMemberAccountCharge,
            (1, JournalDirection.Debit, chart.Receivable),
            (2, JournalDirection.Credit, chart.Revenue));
        await Map(FinancialPostingRuleCodes.MemberAccountPayment,
            (1, JournalDirection.Debit, chart.Cash),
            (2, JournalDirection.Credit, chart.Receivable));
        await Map(FinancialPostingRuleCodes.SupplierInvoice,
            (1, JournalDirection.Debit, chart.Expense),
            (2, JournalDirection.Credit, chart.Payable));
        await Map(FinancialPostingRuleCodes.SupplierPayment,
            (1, JournalDirection.Debit, chart.Payable),
            (2, JournalDirection.Credit, chart.Cash));
    }

    private async Task<FinancialPeriodDto> OpenCurrentPeriodAsync()
    {
        var (year, month) = NextPeriodParts();
        using var scope = Services.CreateScope();
        var periods = scope.ServiceProvider.GetRequiredService<IFinancialPeriodService>();

        return await periods.OpenAsync(
            new OpenFinancialPeriodInput(year, month, UniquePrefix("D6 period")),
            "user-1");
    }

    /// <summary>
    /// The shared fixture runs with bootstrap disabled, so the starter currency and the
    /// D6 posting-rule catalogue that production startup seeds are inserted here. Finance
    /// refuses to post against an unknown currency or an unconfigured rule, which is
    /// exactly the behaviour under test.
    /// </summary>
    private async Task EnsureStarterCurrencyAsync()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        if (await context.Currencies.AnyAsync(c => c.Code == "ZAR"))
        {
            return;
        }

        context.Currencies.Add(new CurrencyEntity
        {
            Code = "ZAR",
            Name = "South African Rand",
            Symbol = "R",
            DecimalPlaces = 2,
            IsDefault = true,
            IsActive = true,
        });
        await context.SaveChangesAsync();
    }

    private async Task EnsurePostingRulesAsync()
    {
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<FinanceReferenceSeeder>();

        // Idempotent, so calling it from every test is cheap and also proves idempotency.
        await seeder.SeedAsync();
    }

    private async Task<Guid> CreateMemberAsync()
    {
        var tag = UniquePrefix("FIN");
        var personId = Guid.CreateVersion7();
        var memberId = Guid.CreateVersion7();

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        context.Persons.Add(new PersonEntity
        {
            Id = personId,
            LegalName = $"Finance {tag}",
            PreferredName = tag,
            AddressLine1 = "1 Test Street",
            City = "Cape Town",
            Region = "Western Cape",
            PostalCode = "8001",
            Country = "ZA",
        });
        context.Members.Add(new MemberEntity
        {
            Id = memberId,
            PersonId = personId,
            MemberNumber = tag,
            Status = MemberStatus.Active,
        });
        await context.SaveChangesAsync();
        return memberId;
    }

    private async Task<string> AccountCodeAsync(Guid accountId)
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        return (await context.FinancialAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId)).Code;
    }

    private async Task<string> AccountReferenceAsync(Guid accountId)
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        return (await context.FinancialAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId)).Reference;
    }

    private async Task<Guid> CreateSupplierAsync()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var supplier = new SupplierEntity
        {
            SupplierCode = UniquePrefix("SUP"),
            Name = "Test Supplier",
        };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }
}
