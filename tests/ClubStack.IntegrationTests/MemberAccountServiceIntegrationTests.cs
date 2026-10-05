using ClubStack.Application.Accounts;
using ClubStack.Application.Exceptions;
using ClubStack.Domain.Accounts;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D3 member account coverage on the shared PostgreSQL fixture: ACC-001..014 via
/// <see cref="IMemberAccountService"/> (derived balances, charges, payments with
/// allocations, credits, adjustments, reversals, statements, aging) and the
/// PAY-005 duplicate-identifier guard.
/// </summary>
[Collection("Database")]
public sealed class MemberAccountServiceIntegrationTests : DatabaseTestBase
{
    public MemberAccountServiceIntegrationTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task First_lookup_creates_active_account_with_unique_ACC_number()
    {
        var memberId = await CreateActiveMemberAsync("ACCS");

        var first = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .GetAccountForMemberAsync(memberId, default));
        Assert.Equal("Active", first.Status);
        Assert.True(MemberAccountNumber.IsValidFormat(first.AccountNumber));
        Assert.StartsWith("ACC", first.AccountNumber, StringComparison.Ordinal);
        Assert.Equal("ZAR", first.CurrencyCode);
        Assert.Equal(0m, first.Balance.AmountDue);

        // The same member always resolves to the same account (ACC-001 singleton).
        var second = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .GetAccountForMemberAsync(memberId, default));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.AccountNumber, second.AccountNumber);
    }

    [Fact]
    public async Task Charge_payment_allocation_flow_maintains_derived_balance()
    {
        var accountId = await CreateAccountAsync("CHGP");

        var charge = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().ChargeAsync(
            accountId,
            new ChargeInput("Meal charge", 500m, "ZAR", null, null, null, null),
            Actor(),
            default));
        Assert.Equal("Charge", charge.TransactionType);
        Assert.Equal(500m, charge.Amount);
        Assert.True(charge.IsDebit);
        Assert.Equal("Posted", charge.Status);

        var balanceAfterCharge = await BalanceAsync(accountId);
        Assert.Equal(500m, balanceAfterCharge.AmountDue);

        var payment = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().RecordPaymentAsync(
            accountId,
            new PaymentInput(
                "Cash payment",
                300m,
                "ZAR",
                "CASH",
                $"ext-{Guid.NewGuid():N}",
                null,
                new[] { new PaymentAllocationInput(charge.Id, 300m) }),
            Actor(),
            default));
        Assert.Equal("Payment", payment.TransactionType);
        Assert.Equal(300m, payment.Amount);
        Assert.NotNull(payment.PaymentMethodCode);
        Assert.Equal("Recorded", payment.PaymentState);

        var balanceAfterPayment = await BalanceAsync(accountId);
        Assert.Equal(200m, balanceAfterPayment.AmountDue);
        Assert.Equal(0m, balanceAfterPayment.UnallocatedPayments);

        var transactions = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .SearchTransactionsAsync(new AccountTransactionSearchInput(accountId), default));
        Assert.Equal(2, transactions.TotalCount);
        Assert.Contains(transactions.Items, t => t.TransactionType == "Charge");
        Assert.Contains(transactions.Items, t => t.TransactionType == "Payment");
    }

    [Fact]
    public async Task Payment_without_allocation_leaves_unallocated_balance()
    {
        var accountId = await CreateAccountAsync("UNAL");
        await ChargeAsync(accountId, 1000m, "Room service");

        var account = await AccountAsync(accountId);
        await Scope(sc => sc.GetRequiredService<IMemberAccountService>().RecordPaymentAsync(
            accountId,
            new PaymentInput("Unallocated payment", 400m, "ZAR", "CASH", $"ext-{Guid.NewGuid():N}", null, null),
            Actor(),
            default));

        var balance = await BalanceAsync(accountId);
        Assert.Equal(600m, balance.AmountDue);
        Assert.Equal(400m, balance.UnallocatedPayments);
        Assert.Equal(0m, balance.CreditsBalance);
    }

    [Fact]
    public async Task Duplicate_external_reference_is_rejected()
    {
        var accountId = await CreateAccountAsync("DUXD");
        const string externalReference = "EXT-PAY-001";

        await Scope(sc => sc.GetRequiredService<IMemberAccountService>().RecordPaymentAsync(
            accountId,
            new PaymentInput("First", 100m, "ZAR", "CASH", externalReference, null, null),
            Actor(),
            default));

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .RecordPaymentAsync(
                accountId,
                new PaymentInput("Duplicate", 100m, "ZAR", "CASH", externalReference, null, null),
                Actor(),
                default)));
    }

    [Fact]
    public async Task Allocation_cannot_exceed_outstanding_charge()
    {
        var accountId = await CreateAccountAsync("ALOX");
        var charge = await ChargeAsync(accountId, 700m, "Bar tab");

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .RecordPaymentAsync(
                accountId,
                new PaymentInput(
                    "Over-allocation",
                    1000m,
                    "ZAR",
                    "CASH",
                    $"ext-{Guid.NewGuid():N}",
                    null,
                    new[] { new PaymentAllocationInput(charge.Id, 1000m) }),
                Actor(),
                default)));

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .RecordPaymentAsync(
                accountId,
                new PaymentInput(
                    "Allocation exceeds payment",
                    100m,
                    "ZAR",
                    "CASH",
                    $"ext-{Guid.NewGuid():N}",
                    null,
                    new[] { new PaymentAllocationInput(charge.Id, 200m) }),
                Actor(),
                default)));
    }

    [Fact]
    public async Task Adjustment_reversals_and_ledger_invariants_are_enforced()
    {
        var accountId = await CreateAccountAsync("ADJS");
        var charge = await ChargeAsync(accountId, 1000m, "Charge");
        var payment = await RecordPaymentAsync(accountId, 1000m, $"ext-{Guid.NewGuid():N}");

        // Negative adjustment reduces the balance (a memo correction, ACC-006).
        var adjustment = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().AdjustAsync(
            accountId,
            new AdjustmentInput("Correction", -200m, "System correction", null),
            Actor(),
            default));
        Assert.Equal("Adjustment", adjustment.TransactionType);
        Assert.Equal(-200m, adjustment.SignedAmount);

        var balance = await BalanceAsync(accountId);
        Assert.Equal(-200m, balance.AmountDue);

        // Only payments and credits may be reversed.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ReverseAsync(accountId, charge.Id, new ReversalInput("n/a"), Actor(), default)));

        // Reversing a payment restores the amount due contribution (compensating entry).
        var reversal = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ReverseAsync(accountId, payment.Id, new ReversalInput("Duplicate payment"), Actor(), default));
        Assert.Equal("Reversal", reversal.TransactionType);
        Assert.Equal(payment.Id, reversal.ReverseOfTransactionId);

        var transactions = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .SearchTransactionsAsync(new AccountTransactionSearchInput(accountId, PageSize: 100), default));
        var original = transactions.Items.Single(t => t.Id == payment.Id);
        Assert.Equal("Reversed", original.Status);

        // Reversed original excluded from the derived balance; reversal adds +1000.
        var afterReversal = await BalanceAsync(accountId);
        Assert.Equal(800m, afterReversal.AmountDue);

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ReverseAsync(accountId, payment.Id, new ReversalInput("Again"), Actor(), default)));
        _ = charge;
    }

    [Fact]
    public async Task Credit_reduces_amount_due_and_reversal_restores_it()
    {
        var accountId = await CreateAccountAsync("CRDT");
        var charge = await ChargeAsync(accountId, 600m, "Charge");
        _ = charge;

        var credit = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().CreateCreditAsync(
            accountId,
            new CreditInput("Goodwill credit", 250m, "ZAR", $"crt-{Guid.NewGuid():N}", null, null),
            Actor(),
            default));
        Assert.Equal("Credit", credit.TransactionType);

        var balance = await BalanceAsync(accountId);
        Assert.Equal(350m, balance.AmountDue);
        Assert.Equal(250m, balance.CreditsBalance);

        var reversal = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ReverseAsync(accountId, credit.Id, new ReversalInput("Credit applied in error"), Actor(), default));
        Assert.Equal(credit.Id, reversal.ReverseOfTransactionId);

        var afterReversal = await BalanceAsync(accountId);
        Assert.Equal(600m, afterReversal.AmountDue);
        Assert.Equal(0m, afterReversal.CreditsBalance);
    }

    [Fact]
    public async Task Statement_reports_period_opening_closing_and_transactions()
    {
        var accountId = await CreateAccountAsync("STMT");
        await ChargeAsync(accountId, 400m, "Statement charge");
        await RecordPaymentAsync(accountId, 150m, $"ext-{Guid.NewGuid():N}");

        var statement = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().GenerateStatementAsync(
            accountId,
            new StatementPeriodInput(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)),
            Actor(),
            default));

        Assert.Equal("STMT", statement.StatementNumber[..4]);
        Assert.Contains(statement.StatementNumber, statement.StatementNumber, StringComparison.Ordinal);
        Assert.Equal(0m, statement.OpeningBalance);
        Assert.Equal(250m, statement.ClosingBalance);
        Assert.Equal(2, statement.Transactions.Count);
        Assert.Equal(statement.ClosingBalance, statement.CurrentBalance);

        // Statements are persisted as generated snapshots.
        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var saved = await context.AccountStatements.AsNoTracking()
                .SingleAsync(s => s.StatementNumber == statement.StatementNumber);
            Assert.Equal(statement.OpeningBalance, saved.OpeningBalance);
            Assert.Equal(statement.ClosingBalance, saved.ClosingBalance);
        }
    }

    [Fact]
    public async Task Aging_places_outstanding_charges_in_thirty_day_buckets()
    {
        var accountId = await CreateAccountAsync("AGNG");
        var current = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().ChargeAsync(
            accountId,
            new ChargeInput("Current charge", 100m, "ZAR", null, null, null, DateTimeOffset.UtcNow),
            Actor(),
            default));
        _ = current;
        await Scope(sc => sc.GetRequiredService<IMemberAccountService>().ChargeAsync(
            accountId,
            new ChargeInput("Overdue charge", 50m, "ZAR", null, null, null, DateTimeOffset.UtcNow.AddDays(-45)),
            Actor(),
            default));

        var aging = await Scope(sc => sc.GetRequiredService<IMemberAccountService>().GetAgingAsync(accountId, default));
        Assert.Equal(150m, aging.TotalOutstanding);
        Assert.Equal(2, aging.OpenItemCount);
        Assert.Equal(30, aging.DaysOutstandingThreshold);

        var currentBucket = Assert.Single(aging.Buckets, b => b.Label == "Current");
        Assert.Equal(100m, currentBucket.OutstandingAmount);
        var overdueBucket = Assert.Single(aging.Buckets, b => b.Label == "30+");
        Assert.Equal(50m, overdueBucket.OutstandingAmount);

        Assert.Contains(aging.Items, i => i.Description == "Overdue charge" && i.DaysOutstanding >= 45);

        var overdue = (await BalanceAsync(accountId)).OverdueBalance;
        Assert.Equal(50m, overdue);
    }

    [Fact]
    public async Task Suspended_or_closed_account_cannot_be_charged()
    {
        var accountId = await CreateAccountAsync("STAT");
        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var entity = await context.MemberAccounts.SingleAsync(a => a.Id == accountId);
            entity.Status = MemberAccountStatus.Suspended;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        var ex = await Record.ExceptionAsync(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ChargeAsync(accountId, new ChargeInput("Blocked", 10m, "ZAR", null, null, null, null), Actor(), default)));
        Assert.NotNull(ex);
        Assert.IsType<ConflictException>(ex);

        var balance = await BalanceAsync(accountId);
        Assert.Equal(0m, balance.AmountDue);
    }

    [Fact]
    public async Task Currency_mismatch_and_non_positive_amounts_are_rejected()
    {
        var accountId = await CreateAccountAsync("CURN");

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ChargeAsync(accountId, new ChargeInput("Wrong currency", 100m, "USD", null, null, null, null), Actor(), default)));

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ChargeAsync(accountId, new ChargeInput("Zero amount", 0m, "ZAR", null, null, null, null), Actor(), default)));

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .AdjustAsync(accountId, new AdjustmentInput("Zero adjustment", 0m, "n/a", null), Actor(), default)));

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .ChargeAsync(accountId, new ChargeInput(" ", 10m, "ZAR", null, null, null, null), Actor(), default)));
    }

    // ---- helpers ----

    private async Task<T> Scope<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private async Task Scope(Func<IServiceProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    private async Task<Guid> CreateActiveMemberAsync(string tag)
    {
        await Scope(sc => sc.GetRequiredService<IReferenceDataSeeder>().SeedAsync());

        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var category = await provider.GetRequiredService<ClubStack.Application.Membership.IMembershipCategoryService>()
            .CreateCategoryAsync(NewCategoryInput(tag), default);
        var person = await provider.GetRequiredService<ClubStack.Application.Membership.IMembershipDirectoryService>()
            .CreatePersonAsync(
                new ClubStack.Application.Membership.CreatePersonInput(
                    $"Acc {tag} {Guid.NewGuid():N}",
                    string.Empty,
                    $"{tag}{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                    null,
                    $"acc-{tag}-{Guid.NewGuid():N}@clubstack.test",
                    $"07{Guid.NewGuid():N}"[..10],
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                default);

        var lifecycle = provider.GetRequiredService<ClubStack.Application.Membership.IMembershipLifecycleService>();
        var application = await lifecycle.CreateApplicationAsync(
            new ClubStack.Application.Membership.CreateMembershipApplicationInput(person.Person.Id, category.Id, "Sponsor", string.Empty, "Acc", "ID"),
            default);
        application = await lifecycle.SubmitApplicationAsync(application.Id, Actor(), default);
        application = await lifecycle.StartReviewApplicationAsync(application.Id, Actor(), default);
        application = await lifecycle.ApproveApplicationAsync(application.Id, Actor(), default);

        var memberships = await lifecycle.GetMembershipsForMemberAsync(application.MemberId!.Value, default);
        await lifecycle.ActivateMembershipAsync(Assert.Single(memberships).Id, Actor(), default);
        return application.MemberId.Value;
    }

    private async Task<Guid> CreateAccountAsync(string tag)
    {
        var memberId = await CreateActiveMemberAsync(tag);
        var account = await Scope(sc => sc.GetRequiredService<IMemberAccountService>()
            .GetAccountForMemberAsync(memberId, default));
        return account.Id;
    }

    private async Task<AccountTransactionDto> ChargeAsync(Guid accountId, decimal amount, string description)
        => await Scope(sc => sc.GetRequiredService<IMemberAccountService>().ChargeAsync(
            accountId,
            new ChargeInput(description, amount, "ZAR", null, null, null, null),
            Actor(),
            default));

    private async Task<AccountTransactionDto> RecordPaymentAsync(Guid accountId, decimal amount, string externalReference)
        => await Scope(sc => sc.GetRequiredService<IMemberAccountService>().RecordPaymentAsync(
            accountId,
            new PaymentInput("Payment", amount, "ZAR", "CASH", externalReference, null, null),
            Actor(),
            default));

    private async Task<AccountBalanceDto> BalanceAsync(Guid accountId)
        => await Scope(sc => sc.GetRequiredService<IMemberAccountService>().GetBalanceAsync(accountId, default));

    private async Task<MemberAccountDto> AccountAsync(Guid accountId)
        => await Scope(sc => sc.GetRequiredService<IMemberAccountService>().GetAccountAsync(accountId, default));

    private static ClubStack.Application.Membership.MembershipCategoryInput NewCategoryInput(string tag) => new(
        UniquePrefix(tag),
        $"Category {tag}",
        "Test category",
        "Eligibility",
        12,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        IsActive: true);

    private static string Actor() => "d3-account-tests";
}