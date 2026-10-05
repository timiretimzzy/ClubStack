using ClubStack.Application.Finance;
using ClubStack.Application.Golf;
using ClubStack.Application.Inventory;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using ClubStack.Domain.Golf;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Finance;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// Golf fixtures shared by the D7 suites. Each builder returns a real, distinct set of
/// rows per call so tests never contend for the same tee slot, which is what lets the
/// suites run in parallel against the one shared PostgreSQL database.
/// </summary>
internal sealed class GolfTestData
{
    private const string Currency = "ZAR";

    private readonly IServiceProvider _services;
    private readonly IDbContextFactory<ClubStackDbContext> _contexts;

    public GolfTestData(IServiceProvider services, IDbContextFactory<ClubStackDbContext> contexts)
    {
        _services = services;
        _contexts = contexts;
    }

    public static GolfTestData Create(IServiceProvider services, IDbContextFactory<ClubStackDbContext> contexts)
        => new(services, contexts);

    public string MoneyCurrency => Currency;

    private static string Tag(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 8)].ToUpperInvariant();

    // ---------------------------------------------------------------------
    // People
    // ---------------------------------------------------------------------

    /// <summary>A D2 member, created directly because golf never creates one.</summary>
    public async Task<Guid> CreateMemberAsync()
    {
        var tag = Tag("GLF");
        var personId = Guid.CreateVersion7();
        var memberId = Guid.CreateVersion7();

        await using var context = await _contexts.CreateDbContextAsync();
        context.Persons.Add(new PersonEntity
        {
            Id = personId,
            LegalName = $"Golf {tag}",
            PreferredName = tag,
            AddressLine1 = "1 Golf Street",
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

    public async Task<Guid[]> CreateMembersAsync(int count)
    {
        var members = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            members.Add(await CreateMemberAsync());
        }

        return [.. members];
    }

    /// <summary>A golf guest record created through the real service (GOLF-008).</summary>
    public async Task<Guid> CreateGuestAsync(Guid? hostMemberId = null, string? fullName = null)
    {
        using var scope = _services.CreateScope();
        var guests = scope.ServiceProvider.GetRequiredService<IGolfGuestService>();

        var guest = await guests.CreateGuestAsync(
            new CreateGolfGuestInput(fullName ?? $"Visitor {Tag("GUE")}", "visitor@example.test", hostMemberId, null),
            Actor,
            default);

        return guest.Id;
    }

    // ---------------------------------------------------------------------
    // D4 catalogue: the only source of golf prices
    // ---------------------------------------------------------------------

    public async Task<Guid> CreateFeeProductAsync(decimal standardAmount, decimal? memberAmount = null)
    {
        await EnsureDefaultCurrencyAsync();

        using var scope = _services.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<IProductCategoryService>();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();

        var category = await categories.CreateAsync(new CreateCategoryInput(Tag("GCAT"), "Golf fees", null), Actor, default);
        var product = await products.CreateAsync(
            new CreateProductInput(
                Tag("GFP"),
                "Green fee",
                category.Id,
                "Each",
                null,
                false,
                0m,
                standardAmount,
                null,
                null,
                Currency),
            Actor,
            default);

        if (memberAmount is { } member)
        {
            // The catalogue service publishes the Standard price; the Member price row is
            // written here so the D5 resolution order (Member, then Standard) can be
            // exercised exactly as a club would configure it.
            await using var context = await _contexts.CreateDbContextAsync();
            context.ProductPrices.Add(new ProductPriceEntity
            {
                ProductId = product.Id,
                PriceType = "Member",
                CurrencyCode = Currency,
                Amount = member,
                EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await context.SaveChangesAsync();
        }

        return product.Id;
    }

    // ---------------------------------------------------------------------
    // Course, tees, slots
    // ---------------------------------------------------------------------

    public async Task<Guid> CreateCourseAsync(Guid? greenFeeProductId = null, Guid? guestFeeProductId = null)
    {
        await EnsureDefaultCurrencyAsync();

        using var scope = _services.CreateScope();
        var courses = scope.ServiceProvider.GetRequiredService<IGolfCourseService>();

        var course = await courses.CreateCourseAsync(
            new CreateGolfCourseInput(
                Tag("CRS"),
                "Golf test course",
                18,
                Currency,
                "D7 fixture",
                Enumerable.Range(1, 18)
                    .Select(n => new CreateGolfHoleInput(n, 4, 400 + n, $"Hole {n}"))
                    .ToList()),
            Actor,
            default);

        await courses.CreateTeeAsync(
            course.Id,
            new CreateGolfTeeInput(
                Tag("TEE"),
                "Championship",
                4,
                false,
                true,
                null,
                null,
                greenFeeProductId,
                guestFeeProductId,
                null),
            Actor,
            default);

        return course.Id;
    }

    /// <summary>
    /// Creates a tee with explicit restriction dimensions, so the GOLF-012 tests can
    /// configure members-only, guest-hostile, minimum and maximum player counts.
    /// </summary>
    public async Task<Guid> CreateTeeAsync(
        Guid courseId,
        int capacity = 4,
        bool membersOnly = false,
        bool guestsAllowed = true,
        int? minimumPlayers = null,
        int? maximumPlayers = null,
        Guid? greenFeeProductId = null,
        Guid? guestFeeProductId = null)
    {
        using var scope = _services.CreateScope();
        var courses = scope.ServiceProvider.GetRequiredService<IGolfCourseService>();

        var tee = await courses.CreateTeeAsync(
            courseId,
            new CreateGolfTeeInput(
                Tag("TEE"),
                "Championship",
                capacity,
                membersOnly,
                guestsAllowed,
                minimumPlayers,
                maximumPlayers,
                greenFeeProductId,
                guestFeeProductId,
                null),
            Actor,
            default);

        return tee.Id;
    }

    /// <summary>A course, a tee and one slot in a single call: the common fixture.</summary>
    public async Task<GolfFixture> CreateBookableAsync(
        int capacity = 4,
        DateOnly? date = null,
        Guid? greenFeeProductId = null,
        Guid? guestFeeProductId = null)
    {
        var courseId = await CreateCourseAsync(greenFeeProductId, guestFeeProductId);
        var teeId = await CreateTeeAsync(courseId, capacity, greenFeeProductId: greenFeeProductId, guestFeeProductId: guestFeeProductId);

        using var scope = _services.CreateScope();
        var slots = scope.ServiceProvider.GetRequiredService<IGolfTeeSlotService>();
        var slot = await slots.CreateTeeSlotAsync(
            new CreateGolfTeeSlotInput(teeId, date ?? DefaultDate, new TimeOnly(7, 0), 10, capacity, null),
            Actor,
            default);

        return new GolfFixture(courseId, teeId, slot.Id, slot.Capacity, slot.Date, slot.StartTime);
    }

    /// <summary>
    /// A fixed business date far enough ahead that a test booking is never in the past,
    /// which keeps the "created now" timestamps the charge path relies on consistent.
    /// </summary>
    public static DateOnly DefaultDate => new(2031, 6, 1);

    public string Actor => "d7-golf-tests";

    /// <summary>Runs a golf service call in its own scope, as a request would.</summary>
    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> call)
    {
        using var scope = _services.CreateScope();
        return await call(scope.ServiceProvider);
    }

    /// <summary>The same, for the handful of golf calls that return nothing.</summary>
    public async Task WithScopeAsync(Func<IServiceProvider, Task> call)
    {
        using var scope = _services.CreateScope();
        await call(scope.ServiceProvider);
    }

    /// <summary>Creates a draft booking for the given players.</summary>
    public async Task<GolfBookingDto> CreateBookingAsync(
        Guid teeSlotId,
        Guid bookedByMemberId,
        params GolfBookingPlayerInput[] players)
        => await CreateBookingAsync(teeSlotId, bookedByMemberId, null, players);

    /// <summary>The same, with the retry key a client would send for this create.</summary>
    public async Task<GolfBookingDto> CreateBookingAsync(
        Guid teeSlotId,
        Guid bookedByMemberId,
        string? idempotencyKey,
        params GolfBookingPlayerInput[] players)
        => await WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>().CreateBookingAsync(
            new CreateGolfBookingInput(teeSlotId, bookedByMemberId, players, null, idempotencyKey),
            Actor,
            "d7-golf-correlation",
            default));

    public async Task<GolfBookingDto> ConfirmAsync(Guid bookingId, bool overrideCapacity = false, string? overrideReason = null)
        => await ConfirmAsync(bookingId, overrideCapacity, overrideReason, null);

    /// <summary>The same, with the retry key a client would send for this confirm.</summary>
    public async Task<GolfBookingDto> ConfirmAsync(
        Guid bookingId,
        bool overrideCapacity,
        string? overrideReason,
        string? idempotencyKey)
        => await WithScopeAsync(sp => sp.GetRequiredService<IGolfBookingService>().ConfirmBookingAsync(
            bookingId,
            new ConfirmGolfBookingInput(null, overrideCapacity, overrideReason, idempotencyKey),
            Actor,
            "d7-golf-correlation",
            default));

    public async Task<GolfTeeSlotDto> SlotAsync(Guid teeSlotId)
        => await WithScopeAsync(sp => sp.GetRequiredService<IGolfTeeSlotService>()
            .GetTeeSlotAsync(teeSlotId, default));

    /// <summary>
    /// The D4 catalogue refuses to price anything without a club default currency, so the
    /// D7 fee fixtures install one. Idempotent, because every test in the collection shares
    /// the single database.
    /// </summary>
    private async Task EnsureDefaultCurrencyAsync()
    {
        await using var context = await _contexts.CreateDbContextAsync();
        if (await context.Currencies.AnyAsync(c => c.Code == Currency))
        {
            return;
        }

        context.Currencies.Add(new CurrencyEntity
        {
            Code = Currency,
            Name = "South African Rand",
            Symbol = "R",
            DecimalPlaces = 2,
            IsDefault = true,
            IsActive = true,
        });
        await context.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------
    // D3/D6 finance: the only source of golf postings
    // ---------------------------------------------------------------------

    /// <summary>
    /// Prepares everything the D3 account charge and the D6 receivable need: the starter
    /// currency, the eight authoritative posting rules, an open period for the month the
    /// charge is dated in, and the two accounts the member-account-charge rule maps to.
    ///
    /// <para>No account code is hard-coded in the golf services, so the mapping has to be
    /// configured here exactly as a club would configure it on the finance screens. That
    /// is also what proves the posting is rule-driven rather than golf-specific.</para>
    /// </summary>
    public async Task PreparePostingAsync()
    {
        await EnsureDefaultCurrencyAsync();

        using (var scope = _services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FinanceReferenceSeeder>().SeedAsync();
        }

        await OpenCurrentPeriodAsync();
        await EnsureMemberAccountChargeMappingAsync();
    }

    private async Task OpenCurrentPeriodAsync()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var context = await _contexts.CreateDbContextAsync())
        {
            var exists = await context.FinancialPeriods
                .AnyAsync(p => p.Year == now.Year && p.Month == now.Month);

            if (exists)
            {
                return;
            }
        }

        using var scope = _services.CreateScope();
        var periods = scope.ServiceProvider.GetRequiredService<IFinancialPeriodService>();
        await periods.OpenAsync(
            new OpenFinancialPeriodInput(now.Year, now.Month, $"D7 golf {now:yyyy-MM}"),
            "d7-golf-tests");
    }

    /// <summary>
    /// Maps §14.2 POS member-account charge (Dr member receivable / Cr revenue) onto real
    /// accounts. Upsert is keyed by rule and sequence, so running this from several tests
    /// converges on the same configuration instead of conflicting.
    /// </summary>
    private async Task EnsureMemberAccountChargeMappingAsync()
    {
        using var scope = _services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountService>();
        var tag = Tag("ACC");

        async Task<Guid> Create(string name, FinancialAccountType type, NormalBalance balance)
        {
            var existing = await accounts.SearchAccountsAsync(
                new FinancialAccountSearchInput(null, name, null, null),
                default);

            var match = existing.Items.FirstOrDefault(a => a.Name == name);
            if (match is not null)
            {
                return match.Id;
            }

            var account = await accounts.CreateAccountAsync(
                new CreateFinancialAccountInput(
                    $"{tag}{name[..3].ToUpperInvariant()}",
                    name,
                    type,
                    null,
                    $"{name} ({tag})",
                    true,
                    FinancialCashKind.None,
                    Currency),
                Actor);

            return account.Id;
        }

        var receivable = await Create(
            "Member receivable",
            FinancialAccountType.Asset,
            NormalBalance.Debit);
        var revenue = await Create("Golf green fee revenue", FinancialAccountType.Revenue, NormalBalance.Credit);

        await accounts.UpsertPostingRuleMappingAsync(
            FinancialPostingRuleCodes.PosMemberAccountCharge,
            new UpsertPostingRuleMappingInput(1, JournalDirection.Debit, receivable, null),
            Actor);
        await accounts.UpsertPostingRuleMappingAsync(
            FinancialPostingRuleCodes.PosMemberAccountCharge,
            new UpsertPostingRuleMappingInput(2, JournalDirection.Credit, revenue, null),
            Actor);
    }
}

/// <summary>Identifies a configured course, tee and slot.</summary>
internal sealed record GolfFixture(
    Guid CourseId,
    Guid TeeId,
    Guid TeeSlotId,
    int Capacity,
    DateOnly Date,
    TimeOnly StartTime);
