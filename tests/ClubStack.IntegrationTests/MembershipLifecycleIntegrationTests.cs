using ClubStack.Application.Exceptions;
using ClubStack.Application.Membership;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// D2 Membership integration coverage on the shared PostgreSQL fixture: the full
/// Phase 8 §7.2/§7.3 lifecycle through the application services, unique-constraint
/// enforcement, concurrent member-number allocation, fee assessment semantics and
/// household/dependant relationships.
/// </summary>
[Collection("Database")]
public sealed class MembershipLifecycleIntegrationTests : DatabaseTestBase
{
    public MembershipLifecycleIntegrationTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Application_flows_draft_to_submitted_to_review_to_approved()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("APFA");

        var application = await SubmitAndReviewAsync(personId, categoryId);
        Assert.Equal("UnderReview", application.Status);
        Assert.NotNull(application.SubmittedAtUtc);
        Assert.NotNull(application.ReviewedAtUtc);

        var approved = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ApproveApplicationAsync(application.Id, Actor(), default));
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.MemberId);
        Assert.NotNull(approved.MemberNumber);
        Assert.True(MembershipNumber.IsValidFormat(approved.MemberNumber));
        Assert.StartsWith("MEM", approved.MemberNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Activation_activates_membership_member_and_application()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("ACTV");
        var application = await ApproveAsync(personId, categoryId);

        var memberships = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipsForMemberAsync(application.MemberId!.Value, default));
        var pending = Assert.Single(memberships);
        Assert.Equal(MembershipStatus.Pending, pending.Status);

        var activated = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ActivateMembershipAsync(pending.Id, Actor(), default));
        Assert.Equal(MembershipStatus.Active, activated.Status);
        Assert.NotNull(activated.StartDateUtc);
        Assert.NotNull(activated.EndDateUtc);
        Assert.True(activated.EndDateUtc > activated.StartDateUtc);

        var member = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .GetMemberAsync(application.MemberId!.Value, default));
        Assert.Equal(MemberStatus.Active, member.Status);

        var finalApplication = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetApplicationAsync(application.Id, default));
        Assert.Equal("Activated", finalApplication.Status);
    }

    [Fact]
    public async Task Concurrent_approvals_allocate_unique_member_numbers()
    {
        var (categoryId, _) = await CreateCategoryAndPersonAsync("CCNU");

        const int applicantCount = 16;
        var applicationIds = new Guid[applicantCount];
        for (var i = 0; i < applicantCount; i++)
        {
            applicationIds[i] = await CreateSubmittedApplicationAsync($"Concurrent Applicant {Guid.NewGuid():N}", categoryId);
        }

        var approved = await Task.WhenAll(applicationIds.Select(async applicationId =>
        {
            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IMembershipLifecycleService>()
                .ApproveApplicationAsync(applicationId, Actor(), default);
        }));

        Assert.Equal(applicantCount, approved.Select(a => a.MemberNumber).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(applicantCount, approved.Select(a => a.MemberNumber).Where(MembershipNumber.IsValidFormat).Count());
    }

    [Fact]
    public async Task Suspend_reinstate_and_cancel_are_enforced_and_recorded()
    {
        var (memberId, membershipId) = await ActivateFreshMemberAsync("SUSP");

        var suspended = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .SuspendMembershipAsync(membershipId, "Disciplinary hold", Actor(), default));
        Assert.Equal(MembershipStatus.Suspended, suspended.Status);

        var suspendedMember = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .GetMemberAsync(memberId, default));
        Assert.Equal(MemberStatus.Suspended, suspendedMember.Status);

        // A second suspend while already suspended is an invalid repeat of a completed transition.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .SuspendMembershipAsync(membershipId, "Again", Actor(), default)));

        var reinstated = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ReinstateMembershipAsync(membershipId, "Hold lifted", Actor(), default));
        Assert.Equal(MembershipStatus.Active, reinstated.Status);

        var history = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipHistoryAsync(membershipId, default));
        Assert.Contains(history, h => h.NewState == "Suspended" && h.Reason.Contains("Disciplinary"));
        Assert.Contains(history, h => h.NewState == "Active" && h.Reason.Contains("lifted"));

        var cancelled = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .CancelMembershipAsync(membershipId, "Member resigned", Actor(), default));
        Assert.Equal(MembershipStatus.Cancelled, cancelled.Status);

        var cancelledMember = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .GetMemberAsync(memberId, default));
        Assert.Equal(MemberStatus.Cancelled, cancelledMember.Status);
    }

    [Fact]
    public async Task Renewal_without_suspension_extends_the_period_and_preserves_history()
    {
        var (memberId, membershipId) = await ActivateFreshMemberAsync("RENW");

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var membership = await context.Memberships.SingleAsync(m => m.Id == membershipId);
            membership.Status = MembershipStatus.Expired;
            membership.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        var renewed = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .RenewMembershipAsync(membershipId, Actor(), default));
        Assert.Equal(MembershipStatus.Active, renewed.Status);
        Assert.True(renewed.EndDateUtc > renewed.StartDateUtc);

        var history = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipHistoryAsync(membershipId, default));
        Assert.Contains(history, h => h.NewState == "Active" && h.Reason.StartsWith("Membership renewed", StringComparison.Ordinal));
        _ = memberId;
    }

    [Fact]
    public async Task Transfer_closes_the_source_and_creates_a_successor()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("TRNS");
        var targetCategory = await Scope(sc => sc.GetRequiredService<IMembershipCategoryService>()
            .CreateCategoryAsync(NewCategoryInput("TRGT"), default));

        var application = await ApproveAsync(personId, categoryId);
        var source = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipsForMemberAsync(application.MemberId!.Value, default));
        var sourceMembership = Assert.Single(source);
        Assert.Equal(MembershipStatus.Pending, sourceMembership.Status);

        var transferred = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .TransferMembershipAsync(sourceMembership.Id, targetCategory.Id, Actor(), default));
        Assert.Equal(MembershipStatus.Transferred, transferred.Status);

        var memberships = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipsForMemberAsync(application.MemberId!.Value, default));
        var successor = Assert.Single(memberships, m => m.Status == MembershipStatus.Pending);
        Assert.Equal(targetCategory.Id, successor.CategoryId);
        Assert.Equal(MembershipStatus.Transferred, Assert.Single(memberships, m => m.Id == sourceMembership.Id).Status);
        Assert.Equal(2, memberships.Count);

        var history = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipHistoryAsync(sourceMembership.Id, default));
        Assert.Contains(history, h => h.NewState == "Transferred");
    }

    [Fact]
    public async Task Rejection_cannot_be_followed_by_activation()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("RJCT");
        var application = await SubmitAndReviewAsync(personId, categoryId);

        var rejected = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .RejectApplicationAsync(application.Id, new RejectMembershipApplicationInput("Ineligible"), Actor(), default));
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Ineligible", rejected.RejectionReason);
        Assert.Null(rejected.MemberId);

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ApproveApplicationAsync(application.Id, Actor(), default)));
    }

    [Fact]
    public async Task Fee_assessment_respects_configuration_store_and_due_window()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("FEES");
        await Scope(sc => sc.GetRequiredService<IMembershipCategoryService>().CreateFeeAsync(
            categoryId,
            new MembershipFeeInput("AnnualFee", "Full year", 12000m, "ZAR", IsProrationEnabled: false, IsActive: true),
            default));

        var membershipId = await ActivateNewMembershipAsync(personId, categoryId);

        var assessed = await Scope(sc => sc.GetRequiredService<IMembershipFeeService>().AssessAsync(
            new AssessMembershipFeeInput(membershipId, "AnnualFee", null),
            Actor(),
            default));

        Assert.Equal(12000m, assessed.Amount);
        Assert.False(assessed.IsProrated);
        Assert.Equal("Outstanding", assessed.Status);
        Assert.Equal("ZAR", assessed.CurrencyCode);
        Assert.NotNull(assessed.DueDateUtc);
        Assert.True(assessed.DueDateUtc > assessed.AssessmentDateUtc);

        var adjusted = await Scope(sc => sc.GetRequiredService<IMembershipFeeService>().AdjustAsync(
            assessed.Id,
            new MembershipFeeAdjustmentInput(6000m, "Goodwill adjustment"),
            Actor(),
            default));
        Assert.Equal(6000m, adjusted.Amount);

        var voided = await Scope(sc => sc.GetRequiredService<IMembershipFeeService>().VoidAsync(
            assessed.Id,
            new MembershipFeeVoidInput("Assessed in error"),
            Actor(),
            default));
        Assert.Equal("Voided", voided.Status);

        // A settled assessment cannot be re-adjusted or re-voided.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMembershipFeeService>()
            .AdjustAsync(assessed.Id, new MembershipFeeAdjustmentInput(1m, "Late change"), Actor(), default)));
    }

    [Fact]
    public async Task Fee_assessment_prorates_an_annual_fee_mid_period()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("PROR");
        await Scope(sc => sc.GetRequiredService<IMembershipCategoryService>().CreateFeeAsync(
            categoryId,
            new MembershipFeeInput("AnnualFee", "Annual membership", 12000m, "ZAR", IsProrationEnabled: true, IsActive: true),
            default));

        var membershipId = await ActivateNewMembershipAsync(personId, categoryId);

        await Assert.ThrowsAsync<ValidationException>(() => Scope(sc => sc.GetRequiredService<IMembershipFeeService>()
            .AssessAsync(new AssessMembershipFeeInput(membershipId, "JoiningFee", null), Actor(), default)));

        var detail = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipAsync(membershipId, default));

        var assessed = await Scope(sc => sc.GetRequiredService<IMembershipFeeService>().AssessAsync(
            new AssessMembershipFeeInput(membershipId, "AnnualFee", detail.EndDateUtc!.Value.AddDays(-30)),
            Actor(),
            default));

        Assert.True(assessed.IsProrated);
        var expected = decimal.Round(
            12000m * 30 / (detail.EndDateUtc!.Value - detail.StartDateUtc!.Value).Days,
            4,
            MidpointRounding.ToEven);
        Assert.Equal(expected, assessed.Amount);
    }

    [Fact]
    public async Task Database_rejects_a_duplicate_member_number()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("DUPM");
        var approved = await ApproveAsync(personId, categoryId);

        var secondPerson = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>().CreatePersonAsync(
            new CreatePersonInput(
                "Duplicate Person",
                string.Empty,
                null,
                null,
                $"dup-{Guid.NewGuid():N}@clubstack.test",
                null,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            default));

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var first = await context.Members.SingleAsync(m => m.Id == approved.MemberId!.Value);
            context.Members.Add(new MemberEntity
            {
                PersonId = secondPerson.Person.Id,
                MemberNumber = first.MemberNumber,
                Status = MemberStatus.Prospect,
            });

            var ex = await Record.ExceptionAsync(() => context.SaveChangesAsync());
            Assert.NotNull(ex);
        }
    }

    [Fact]
    public async Task Person_with_active_member_cannot_apply_again()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("DUP2");
        await ActivateNewMembershipAsync(personId, categoryId);

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .CreateApplicationAsync(
                new CreateMembershipApplicationInput(personId, categoryId, null, null, string.Empty, string.Empty),
                default)));
    }

    [Fact]
    public async Task Dependants_and_households_persist_and_guard_invariants()
    {
        var (categoryId, memberPersonId) = await CreateCategoryAndPersonAsync("DEPH");
        var approved = await ApproveAsync(memberPersonId, categoryId);
        var memberId = approved.MemberId!.Value;

        var dependant = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>().CreatePersonAsync(
            new CreatePersonInput(
                "Dependant Person",
                string.Empty,
                null,
                null,
                $"dep-{Guid.NewGuid():N}@clubstack.test",
                null,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            default));

        var added = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>().AddDependantAsync(
            memberId,
            new AddDependantInput(dependant.Person.Id, "Child", "School-aged"),
            default));
        Assert.Equal("Child", added.RelationType);

        // A duplicate dependant assignment is rejected.
        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .AddDependantAsync(memberId, new AddDependantInput(dependant.Person.Id, "Child", string.Empty), default)));

        var household = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .CreateHouseholdAsync(new HouseholdInput("The Family", memberId), default));
        Assert.Equal(memberId, household.PrimaryMemberId);
        Assert.Contains(household.Members, m => m.PersonId == memberPersonId && m.Role == "Primary");

        await Assert.ThrowsAsync<ConflictException>(() => Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .RemoveHouseholdMemberAsync(household.Id, memberPersonId, default)));
    }

    [Fact]
    public async Task Member_number_registry_is_the_identity_singleton()
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync("SING");
        var application = await ApproveAsync(personId, categoryId);
        var memberId = application.MemberId!.Value;

        // Phase 9: identity = Person, status = Membership, category = MembershipCategory.
        var person = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>()
            .GetPersonAsync(personId, default));
        Assert.Equal(memberId, person.MemberId);
        Assert.Equal(application.MemberNumber, person.MemberNumber);

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var memberships = await context.Memberships.CountAsync(m => m.MemberId == memberId);
            Assert.Equal(1, memberships);
        }
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

    private async Task<(Guid CategoryId, Guid PersonId)> CreateCategoryAndPersonAsync(string tag)
    {
        var category = await Scope(sc => sc.GetRequiredService<IMembershipCategoryService>()
            .CreateCategoryAsync(NewCategoryInput(tag), default));

        var person = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>().CreatePersonAsync(
            new CreatePersonInput(
                $"Applicant {tag} {Guid.NewGuid():N}",
                string.Empty,
                $"{tag}{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                null,
                $"{tag.ToLowerInvariant()}-{Guid.NewGuid():N}@clubstack.test",
                $"07{Guid.NewGuid():N}"[..10],
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            default));

        return (category.Id, person.Person.Id);
    }

    private async Task<MembershipApplicationDto> SubmitAndReviewAsync(Guid personId, Guid categoryId)
    {
        var application = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .CreateApplicationAsync(
                new CreateMembershipApplicationInput(personId, categoryId, "Sponsor", string.Empty, "Notes", "ID"),
                default));
        application = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .SubmitApplicationAsync(application.Id, Actor(), default));
        application = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .StartReviewApplicationAsync(application.Id, Actor(), default));
        return application;
    }

    private async Task<MembershipApplicationDto> ApproveAsync(Guid personId, Guid categoryId)
    {
        var application = await SubmitAndReviewAsync(personId, categoryId);
        return await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ApproveApplicationAsync(application.Id, Actor(), default));
    }

    private async Task<Guid> ActivateNewMembershipAsync(Guid personId, Guid categoryId)
    {
        var application = await ApproveAsync(personId, categoryId);
        var memberships = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipsForMemberAsync(application.MemberId!.Value, default));
        var membership = Assert.Single(memberships);
        await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ActivateMembershipAsync(membership.Id, Actor(), default));
        return membership.Id;
    }

    private async Task<Guid> CreateSubmittedApplicationAsync(string personName, Guid categoryId)
    {
        var person = await Scope(sc => sc.GetRequiredService<IMembershipDirectoryService>().CreatePersonAsync(
            new CreatePersonInput(personName, string.Empty, null, null, $"cc-{Guid.NewGuid():N}@clubstack.test", null,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            default));

        var application = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .CreateApplicationAsync(
                new CreateMembershipApplicationInput(person.Person.Id, categoryId, null, null, string.Empty, string.Empty),
                default));
        application = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .SubmitApplicationAsync(application.Id, Actor(), default));
        application = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .StartReviewApplicationAsync(application.Id, Actor(), default));
        return application.Id;
    }

    private async Task<(Guid MemberId, Guid MembershipId)> ActivateFreshMemberAsync(string tag)
    {
        var (categoryId, personId) = await CreateCategoryAndPersonAsync(tag);
        var application = await ApproveAsync(personId, categoryId);
        var memberships = await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .GetMembershipsForMemberAsync(application.MemberId!.Value, default));
        var membership = Assert.Single(memberships);
        await Scope(sc => sc.GetRequiredService<IMembershipLifecycleService>()
            .ActivateMembershipAsync(membership.Id, Actor(), default));
        return (application.MemberId!.Value, membership.Id);
    }

    private static MembershipCategoryInput NewCategoryInput(string tag) => new(
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

    private static string Actor() => "integration-actor";
}