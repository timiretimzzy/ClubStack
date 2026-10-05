using ClubStack.Application.Credentials;
using ClubStack.Application.Exceptions;
using ClubStack.Domain.Credentials;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D3 credential coverage on the shared PostgreSQL fixture: the Phase 8 §7.4
/// lifecycle through <see cref="ICredentialService"/> (issue, activate, suspend,
/// lost/stolen + replacement, expiry) including immutable history and unique
/// identifier enforcement (Phase 2 CRED-001..010).
/// </summary>
[Collection("Database")]
public sealed class CredentialServiceIntegrationTests : DatabaseTestBase
{
    public CredentialServiceIntegrationTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Issue_assigns_CRD_number_and_Issued_state_with_valid_identifier()
    {
        var issued = await Scope(sc => sc.GetRequiredService<ICredentialService>().IssueAsync(
            NewIssueInput("ISSUE-123456"),
            Actor(),
            default));

        Assert.Equal("Issued", issued.Status);
        Assert.True(CredentialNumber.IsValidFormat(issued.CredentialNumber));
        Assert.StartsWith("CRD", issued.CredentialNumber, StringComparison.Ordinal);
        Assert.Equal("MagneticStripe", issued.CredentialType);
        Assert.Equal("ISSUE-123456", issued.CredentialValue);
        Assert.Null(issued.MemberId);
    }

    [Fact]
    public async Task Issue_rejects_unexpected_type_and_bad_identifier()
    {
        await using var scope = Services.CreateAsyncScope();
        var credentials = scope.ServiceProvider.GetRequiredService<ICredentialService>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            credentials.IssueAsync(new IssueCredentialInput("QrCode", "QR-123456", null), Actor(), default));

        await Assert.ThrowsAsync<ValidationException>(() =>
            credentials.IssueAsync(new IssueCredentialInput("MagneticStripe", "bad id", null), Actor(), default));
    }

    [Fact]
    public async Task Issue_rejects_duplicate_identifier_even_across_states()
    {
        var issued = await Scope(sc => sc.GetRequiredService<ICredentialService>().IssueAsync(
            NewIssueInput("DUP-123456"),
            Actor(),
            default));
        _ = issued;

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .IssueAsync(NewIssueInput("dup-123456"), Actor(), default)));
    }

    [Fact]
    public async Task Full_lifecycle_issue_activate_suspend_reactivate_with_history()
    {
        var memberId = await CreateActiveMemberAsync("LSLC");
        var issued = await Scope(sc => sc.GetRequiredService<ICredentialService>().IssueAsync(
            NewIssueInput("CYCLE-12345"),
            Actor(),
            default));

        var activated = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ActivateAsync(issued.Id, new ActivateCredentialInput(memberId), Actor(), default));
        Assert.Equal("Active", activated.Status);
        Assert.Equal(memberId, activated.MemberId);
        Assert.NotNull(activated.ActivatedAtUtc);

        var resolved = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("cycle-12345", default));
        Assert.Equal(activated.Id, resolved.CredentialId);
        Assert.Equal(memberId, resolved.MemberId);

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .SuspendAsync(issued.Id, new SuspendCredentialInput("  "), Actor(), default)));

        var suspended = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .SuspendAsync(issued.Id, new SuspendCredentialInput("Temp hold"), Actor(), default));
        Assert.Equal("Suspended", suspended.Status);

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("CYCLE-12345", default)));

        var reactivated = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ReactivateAsync(issued.Id, new ReactivateCredentialInput("Hold lifted"), Actor(), default));
        Assert.Equal("Active", reactivated.Status);

        var resolvedAgain = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("CYCLE-12345", default));
        Assert.Equal(memberId, resolvedAgain.MemberId);

        var history = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .GetHistoryAsync(issued.Id, default));
        Assert.Contains(history, h => h.NewState == "Issued" && h.PreviousState == "Issued");
        Assert.Contains(history, h => h.NewState == "Active" && h.PreviousState == "Issued");
        Assert.Contains(history, h => h.NewState == "Suspended" && h.Reason.Contains("Temp"));
        Assert.Contains(history, h => h.NewState == "Active" && h.Reason.Contains("Hold lifted"));
    }

    [Fact]
    public async Task Invalid_transitions_are_rejected()
    {
        var memberId = await CreateActiveMemberAsync("INVT");
        var issued = await Scope(sc => sc.GetRequiredService<ICredentialService>().IssueAsync(
            NewIssueInput("INVALID-1"),
            Actor(),
            default));

        // A card that was never activated cannot be suspended.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .SuspendAsync(issued.Id, new SuspendCredentialInput("Hold"), Actor(), default)));

        var activated = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ActivateAsync(issued.Id, new ActivateCredentialInput(memberId), Actor(), default));

        // Activating an active credential is an invalid repeat of a completed transition.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ActivateAsync(activated.Id, new ActivateCredentialInput(memberId), Actor(), default)));

        // Replacing an active credential is not allowed; only lost/stolen.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ReplaceAsync(activated.Id, NewReplaceInput("REP-111111", "Not lost yet"), Actor(), default)));

        // Lost credentials cannot be reactivated, only replaced.
        var lost = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .MarkLostAsync(issued.Id, new LostCredentialInput("Lost on premises"), Actor(), default));
        Assert.Equal("Lost", lost.Status);

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ReactivateAsync(issued.Id, new ReactivateCredentialInput("Found it"), Actor(), default)));

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("INVALID-1", default)));
    }

    [Fact]
    public async Task Replace_retires_lost_card_and_activates_replacement_on_same_member()
    {
        var memberId = await CreateActiveMemberAsync("REPL");
        var lost = await Scope(async sc =>
        {
            var credentials = sc.GetRequiredService<ICredentialService>();
            var card = await credentials.IssueAsync(NewIssueInput("LOST-123456"), Actor(), default);
            await credentials.ActivateAsync(card.Id, new ActivateCredentialInput(memberId), Actor(), default);
            return await credentials.MarkLostAsync(card.Id, new LostCredentialInput("Wallet lost"), Actor(), default);
        });

        var replacement = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ReplaceAsync(lost.Id, NewReplaceInput("NEWCARD-01", "Replacement issued"), Actor(), default));

        Assert.Equal("Active", replacement.Status);
        Assert.Equal(memberId, replacement.MemberId);
        Assert.NotEqual(lost.Id, replacement.Id);
        Assert.True(CredentialNumber.IsValidFormat(replacement.CredentialNumber));
        Assert.NotEqual(lost.CredentialNumber, replacement.CredentialNumber);

        var oldAfter = await Scope(sc => sc.GetRequiredService<ICredentialService>().GetAsync(lost.Id, default));
        Assert.Equal("Replaced", oldAfter.Status);
        Assert.Equal(replacement.Id, oldAfter.ReplacedByCredentialId);
        Assert.Equal(replacement.CredentialNumber, oldAfter.ReplacedByCredentialNumber);

        // The old card no longer resolves; the replacement does.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("LOST-123456", default)));
        var resolved = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("newcard-01", default));
        Assert.Equal(memberId, resolved.MemberId);

        var oldHistory = await Scope(sc => sc.GetRequiredService<ICredentialService>().GetHistoryAsync(lost.Id, default));
        Assert.Contains(oldHistory, h => h.NewState == "Replaced");

        var replacementHistory = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .GetHistoryAsync(replacement.Id, default));
        Assert.Contains(replacementHistory, h => h.NewState == "Active");
    }

    [Fact]
    public async Task Stolen_credential_resolves_only_through_replacement()
    {
        var memberId = await CreateActiveMemberAsync("STLN");
        var stolen = await Scope(async sc =>
        {
            var credentials = sc.GetRequiredService<ICredentialService>();
            var card = await credentials.IssueAsync(NewIssueInput("STOLEN-12"), Actor(), default);
            await credentials.ActivateAsync(card.Id, new ActivateCredentialInput(memberId), Actor(), default);
            return await credentials.MarkStolenAsync(card.Id, new StolenCredentialInput("Reported stolen"), Actor(), default);
        });

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .SuspendAsync(stolen.Id, new SuspendCredentialInput("n/a"), Actor(), default)));

        var replacement = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ReplaceAsync(stolen.Id, NewReplaceInput("STOLEN-RE1", "Replacement"), Actor(), default));
        Assert.Equal("Active", replacement.Status);
        Assert.Equal(memberId, replacement.MemberId);
    }

    [Fact]
    public async Task Expired_credential_is_never_returned_as_active()
    {
        var memberId = await CreateActiveMemberAsync("EXPR");
        var issued = await Scope(sc => sc.GetRequiredService<ICredentialService>().IssueAsync(
            new IssueCredentialInput("MagneticStripe", "EXPIRY-1234", DateTimeOffset.UtcNow.AddMinutes(5)),
            Actor(),
            default));
        var active = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ActivateAsync(issued.Id, new ActivateCredentialInput(memberId), Actor(), default));
        Assert.Equal("Active", active.Status);

        // Date passes.
        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var entity = await context.Credentials.SingleAsync(c => c.Id == issued.Id);
            entity.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            context.Credentials.Update(entity);
            await context.SaveChangesAsync();
        }

        var after = await Scope(sc => sc.GetRequiredService<ICredentialService>().GetAsync(issued.Id, default));
        Assert.Equal("Expired", after.Status);

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("EXPIRY-1234", default)));

        var history = await Scope(sc => sc.GetRequiredService<ICredentialService>().GetHistoryAsync(issued.Id, default));
        Assert.Contains(history, h => h.NewState == "Expired" && h.ChangedByUserId == "system" && h.Reason.Contains("expiry", StringComparison.OrdinalIgnoreCase));

        // The expired card is filtered out of Active searches.
        var search = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .SearchAsync(new CredentialSearchInput(Status: "Active"), default));
        Assert.DoesNotContain(search.Items, i => i.Id == issued.Id);
    }

    [Fact]
    public async Task Resolve_rejects_unknown_and_unassigned_cards()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("DEFINITELY-UNKNOWN", default)));

        // Issued (unassigned) card does not resolve.
        var issued = await Scope(sc => sc.GetRequiredService<ICredentialService>()
            .IssueAsync(NewIssueInput("UNASSIGN-1"), Actor(), default));
        _ = issued;
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<ICredentialService>()
            .ResolveAsync("UNASSIGN-1", default)));
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

    private static IssueCredentialInput NewIssueInput(string value) => new("MagneticStripe", value, null);

    private static ReplaceCredentialInput NewReplaceInput(string value, string reason) => new("MagneticStripe", value, null, reason);

    private async Task<Guid> CreateActiveMemberAsync(string tag)
    {
        await Scope(sc => sc.GetRequiredService<ClubStack.Infrastructure.Identity.IReferenceDataSeeder>().SeedAsync());

        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var category = await provider.GetRequiredService<ClubStack.Application.Membership.IMembershipCategoryService>()
            .CreateCategoryAsync(NewCategoryInput(tag), default);
        var person = await provider.GetRequiredService<ClubStack.Application.Membership.IMembershipDirectoryService>()
            .CreatePersonAsync(
                new ClubStack.Application.Membership.CreatePersonInput(
                    $"Cred {tag} {Guid.NewGuid():N}",
                    string.Empty,
                    $"{tag}{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                    null,
                    $"cred-{tag}-{Guid.NewGuid():N}@clubstack.test",
                    $"07{Guid.NewGuid():N}"[..10],
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                default);

        var lifecycle = provider.GetRequiredService<ClubStack.Application.Membership.IMembershipLifecycleService>();
        var application = await lifecycle.CreateApplicationAsync(
            new ClubStack.Application.Membership.CreateMembershipApplicationInput(person.Person.Id, category.Id, "Sponsor", string.Empty, "Cred", "ID"),
            default);
        application = await lifecycle.SubmitApplicationAsync(application.Id, Actor(), default);
        application = await lifecycle.StartReviewApplicationAsync(application.Id, Actor(), default);
        application = await lifecycle.ApproveApplicationAsync(application.Id, Actor(), default);

        var memberships = await lifecycle.GetMembershipsForMemberAsync(application.MemberId!.Value, default);
        await lifecycle.ActivateMembershipAsync(Assert.Single(memberships).Id, Actor(), default);
        return application.MemberId.Value;
    }

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

    private static string Actor() => "d3-credential-tests";
}