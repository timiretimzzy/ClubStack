using ClubStack.Application.Accounts;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Finance;
using ClubStack.Application.Golf;
using ClubStack.Domain.Accounts;
using ClubStack.Domain.Finance;
using ClubStack.Domain.Common;
using ClubStack.Domain.Golf;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D7 financial behaviour: GOLF-016/017 (a configured fee becomes a charge, and a charge
/// becomes money), XMOD-004 (the charge reaches the member account), XMOD-005 (the charge
/// produces a receivable and a journal through the configured D6 rule), and the guarantee
/// that golf itself never posts a journal or invents a currency.
/// </summary>
[Collection("Database")]
public sealed class D7GolfChargeTests : DatabaseTestBase
{
    public D7GolfChargeTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private GolfTestData Data => GolfTestData.Create(Services, Fixture.ContextFactory);

    [Fact]
    public async Task Posting_charges_debits_the_member_account_and_raises_one_receivable()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 450m, memberAmount: 380m);
        var guestFee = await data.CreateFeeProductAsync(standardAmount: 500m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee, guestFeeProductId: guestFee);

        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();
        var guest = await data.CreateGuestAsync(hostMemberId: bookedBy);

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId,
            bookedBy,
            new GolfBookingPlayerInput(member, null),
            new GolfBookingPlayerInput(null, guest));
        await data.ConfirmAsync(booking.Id);

        var posted = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default));

        // 380 member + 500 guest.
        Assert.All(posted.Charges, c => Assert.Equal(GolfChargeStatus.Charged, c.Status));
        Assert.Equal(880m, posted.Charges.Sum(c => c.Amount));

        // XMOD-004: one D3 account charge for the whole booking, denominated in the course
        // currency, carrying the golf source so it is traceable back to the booking.
        var account = await data.WithScopeAsync(sp => sp.GetRequiredService<IMemberAccountService>()
            .GetAccountForMemberAsync(bookedBy, default));

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var transaction = await context.AccountTransactions
            .AsNoTracking()
            .SingleAsync(t => t.SourceType == "GolfBooking" && t.SourceId == booking.Id);

        Assert.Equal(880m, transaction.Amount);
        Assert.Equal(data.MoneyCurrency, transaction.CurrencyCode);
        Assert.Equal(booking.Reference, transaction.Reference);
        Assert.Equal(account.Id, transaction.AccountId);
        Assert.Equal(AccountTransactionType.Charge, transaction.TransactionType);

        // XMOD-005: the receivable the posting produced.
        var receivable = await context.AccountsReceivable
            .AsNoTracking()
            .SingleAsync(r => r.SourceType == "GolfBooking" && r.SourceId == booking.Id);

        Assert.Equal(880m, receivable.OriginalAmount);
        Assert.Equal(880m, receivable.OutstandingAmount);
        Assert.Equal(data.MoneyCurrency, receivable.CurrencyCode);
        Assert.Equal(bookedBy, receivable.MemberId);

        // D6 owns the receivable reference; the golf booking is carried as the source
        // reference, which is what makes the document traceable back to the tee sheet.
        Assert.StartsWith("AR-", receivable.Reference, StringComparison.Ordinal);
        Assert.Equal(booking.Reference, receivable.SourceReference);

        // The journal is the D6 engine's work. It carries the golf source, and its lines
        // land on the accounts the club mapped to the §14.2 rule, which is what proves the
        // posting is rule-driven rather than golf-specific.
        var journal = await context.Journals
            .AsNoTracking()
            .SingleAsync(j => j.SourceType == "GolfBooking" && j.SourceId == booking.Id);

        // D6 links the journal from the receivable, so the receivable is what points at it.
        Assert.Equal(journal.Id, receivable.JournalId);
        Assert.NotNull(journal.PostedAtUtc);
        Assert.Equal(data.Actor, journal.PostedByUserId);

        var mapping = await context.FinancialPostingRuleMappings
            .AsNoTracking()
            .Include(m => m.Rule)
            .Where(m => m.Rule.Code == FinancialPostingRuleCodes.PosMemberAccountCharge)
            .OrderBy(m => m.Sequence)
            .ToListAsync();

        Assert.Equal(2, mapping.Count);

        var lines = await context.JournalLines
            .AsNoTracking()
            .Where(l => l.JournalId == journal.Id)
            .OrderBy(l => l.LineNumber)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        for (var i = 0; i < lines.Count; i++)
        {
            Assert.Equal(mapping[i].FinancialAccountId, lines[i].FinancialAccountId);
            Assert.Equal(mapping[i].Direction, lines[i].Direction);
            Assert.Equal(880m, lines[i].Amount);
        }

        // A balanced journal: debits equal credits.
        Assert.Equal(
            lines.Sum(l => l.Direction == JournalDirection.Debit ? l.Amount : 0m),
            lines.Sum(l => l.Direction == JournalDirection.Credit ? l.Amount : 0m));

        Assert.Equal(journal.Id, posted.Charges[0].JournalId);
        Assert.Equal(receivable.Id, posted.Charges[0].ReceivableId);
        Assert.Equal(transaction.Id, posted.Charges[0].AccountTransactionId);
    }

    [Fact]
    public async Task A_charge_cannot_be_posted_twice()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 250m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

        Assert.Contains("already been charged", Flatten(ex), StringComparison.OrdinalIgnoreCase);

        // One charge, one transaction: the refusal is a refusal.
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.AccountTransactions.CountAsync(t => t.SourceId == booking.Id));
    }

    [Fact]
    public async Task A_draft_or_cancelled_booking_never_reaches_the_account()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 300m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var draft = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(draft.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

        Assert.Contains("Draft", Flatten(ex), StringComparison.OrdinalIgnoreCase);

        await data.ConfirmAsync(draft.Id);
        await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .CancelBookingAsync(draft.Id, new CancelGolfBookingInput("Weather"), data.Actor, default));

        var cancelled = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(draft.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

        Assert.Contains("Cancelled", Flatten(cancelled), StringComparison.OrdinalIgnoreCase);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await context.AccountTransactions.CountAsync(t => t.SourceId == draft.Id));
    }

    [Fact]
    public async Task A_configured_additional_charge_is_priced_from_the_catalogue_and_posted_with_the_rest()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 400m);
        var buggy = await data.CreateFeeProductAsync(standardAmount: 50m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        var withBuggy = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .AddChargeAsync(
                booking.Id,
                new AddGolfChargeInput(buggy, 1, null, "Buggy hire"),
                data.Actor,
                default));

        var buggyCharge = Assert.Single(withBuggy.Charges, c => c.ChargeType == GolfChargeType.Additional);
        Assert.Equal(50m, buggyCharge.Amount);
        Assert.Equal(GolfChargeStatus.Pending, buggyCharge.Status);
        Assert.Equal("Buggy hire", buggyCharge.Description);

        // 400 green fee + 50 buggy.
        var posted = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default));

        Assert.Equal(450m, posted.Charges.Sum(c => c.Amount));
        Assert.All(posted.Charges, c => Assert.Equal(GolfChargeStatus.Charged, c.Status));

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var transaction = await context.AccountTransactions
            .AsNoTracking()
            .SingleAsync(t => t.SourceId == booking.Id);

        Assert.Equal(450m, transaction.Amount);
    }

    [Fact]
    public async Task A_waived_charge_is_not_posted()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 200m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        var confirmed = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>()
            .GetBookingAsync(booking.Id, default));
        var charge = Assert.Single(confirmed.Charges);

        var waived = await data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
            .WaiveChargeAsync(booking.Id, charge.Id, "Member complaint", data.Actor, default));

        var waivedCharge = Assert.Single(waived.Charges);
        Assert.Equal(GolfChargeStatus.Waived, waivedCharge.Status);

        // The reason lives on the charge row: the audit detail is stored, not just returned.
        await using (var verify = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var stored = await verify.GolfCharges.AsNoTracking().SingleAsync(c => c.Id == charge.Id);
            Assert.Equal("Member complaint", stored.WaivedReason);
        }

        // Nothing is left to post, so the booking cannot produce money and none does.
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

        Assert.Contains("no pending charges", Flatten(ex), StringComparison.OrdinalIgnoreCase);

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await context.AccountTransactions.CountAsync(t => t.SourceId == booking.Id));
    }

    [Fact]
    public async Task A_charge_in_a_foreign_currency_is_refused_rather_than_converted()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 100m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        // Golf never converts an amount and never mixes currencies, so a charge line that
        // disagrees with the booking's currency is exactly the state the posting guard
        // exists for. It is written directly here because the service refuses to create it.
        await using (var edit = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var charge = await edit.GolfCharges.SingleAsync(c => c.BookingId == booking.Id);
            charge.CurrencyCode = "EUR";
            await edit.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

        Assert.Contains("cannot be posted", Flatten(ex), StringComparison.OrdinalIgnoreCase);

        await using var verify = await Fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await verify.AccountTransactions.CountAsync(t => t.SourceId == booking.Id));
    }

    [Fact]
    public async Task A_failed_posting_leaves_no_partial_money_behind()
    {
        var data = Data;
        await data.PreparePostingAsync();

        var greenFee = await data.CreateFeeProductAsync(standardAmount: 150m);
        var fixture = await data.CreateBookableAsync(greenFeeProductId: greenFee);
        var bookedBy = await data.CreateMemberAsync();
        var member = await data.CreateMemberAsync();

        var booking = await data.CreateBookingAsync(
            fixture.TeeSlotId, bookedBy, new GolfBookingPlayerInput(member, null));
        await data.ConfirmAsync(booking.Id);

        // Remove the §14.2 mapping for the duration of the test, so the posting fails
        // inside the D6 engine *after* the D3 account charge has already been created in
        // the same transaction. Nothing may survive that failure.
        var saved = await TakeMappingAsync();

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(
                () => data.WithScopeAsync(sp => sp.GetRequiredService<IGolfChargeService>()
                    .PostBookingChargesAsync(booking.Id, new PostGolfBookingChargesInput(), data.Actor, "d7-golf-correlation", default)));

            // The transaction is the boundary: the account charge created before the failure
            // is rolled back with the journal, and the charge line stays pending.
            await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
            Assert.Equal(0, await context.AccountTransactions.CountAsync(t => t.SourceId == booking.Id));
            Assert.Equal(0, await context.AccountsReceivable.CountAsync(r => r.SourceId == booking.Id));
            Assert.Equal(0, await context.Journals.CountAsync(j => j.SourceId == booking.Id));
            Assert.Equal(
                GolfChargeStatus.Pending,
                (await context.GolfCharges.AsNoTracking().SingleAsync(c => c.BookingId == booking.Id)).Status);
        }
        finally
        {
            // Restore the shared configuration, because the collection's other tests use it.
            await using (var restore = await Fixture.ContextFactory.CreateDbContextAsync())
            {
                // The saved rows carry a populated Rule navigation from the context that
                // read them, which would make EF try to insert the rule again.
                foreach (var mapping in saved)
                {
                    mapping.Rule = null!;
                }

                restore.FinancialPostingRuleMappings.AddRange(saved);
                await restore.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Removes and returns the §14.2 rule mappings. D6 ships the rules without mappings on
    /// purpose, so this is the honest way to make the posting engine refuse: the failure is
    /// the real "rule is not configured" path rather than an injected fault.
    /// </summary>
    private async Task<List<FinancialPostingRuleMappingEntity>> TakeMappingAsync()
    {
        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var rule = await context.FinancialPostingRules
            .SingleAsync(r => r.Code == FinancialPostingRuleCodes.PosMemberAccountCharge);

        var mappings = await context.FinancialPostingRuleMappings
            .Where(m => m.RuleId == rule.Id)
            .ToListAsync();

        context.FinancialPostingRuleMappings.RemoveRange(mappings);
        await context.SaveChangesAsync();

        return mappings;
    }

    private static string Flatten(Exception exception)
    {
        var text = exception.ToString();

        if (exception is ValidationException validation)
        {
            foreach (var (field, messages) in validation.Errors)
            {
                text += " | " + field + ": " + string.Join("; ", messages);
            }
        }

        var inner = exception.InnerException;
        while (inner is not null)
        {
            text += " | " + inner.Message;
            inner = inner.InnerException;
        }

        return text;
    }
}
