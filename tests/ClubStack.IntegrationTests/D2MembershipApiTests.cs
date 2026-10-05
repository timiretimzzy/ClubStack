using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClubStack.Api;
using ClubStack.Api.Contracts;
using ClubStack.Application.Identity;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// HTTP-level verification of the D2 Membership surface through the real API
/// pipeline (cookies, sessions, permission policies, Problem Details + audit).
/// </summary>
public sealed class D2MembershipApiTests : IAsyncLifetime
{
    private const string UserName = "d2-membership-admin";
    private const string Password = "Str0ng!Pass12";

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        (_connectionString, _databaseName) = await PostgreSqlTestFixture.CreateScratchDatabaseAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var optionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ClubStackDbContext>));
                if (optionsDescriptor is not null)
                {
                    services.Remove(optionsDescriptor);
                }

                services.AddSingleton<DbContextOptions<ClubStackDbContext>>(_ =>
                {
                    var optionsBuilder = new DbContextOptionsBuilder<ClubStackDbContext>();
                    optionsBuilder.UseNpgsql(_connectionString);
                    return optionsBuilder.Options;
                });

                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
            });
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            await provider.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
            await provider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
            await identity.CreateUserAsync(new CreateUserInput(
                UserName, $"{UserName}@example.com", "D2", "Admin", "0000000000",
                Password, ["System Administrator"]));
        }
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _client.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task Anonymous_requests_to_membership_endpoints_are_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/persons")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/membership-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/membership-categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/households")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/membership-fee-assessments")).StatusCode);
    }

    [Fact]
    public async Task Membership_lifecycle_flows_over_http_with_audit_trail()
    {
        await LoginAsync();
        var tag = Guid.NewGuid().ToString("N");
        var category = await CreateCategoryAsync($"G{tag[..8]}".ToUpperInvariant(), "Gold");

        var person = await CreatePersonAsync($"Ada {tag}", $"ada-{tag}@clubstack.test");
        var personId = person.Person.Id;

        var fee = await CreateFeeAsync(category.Id, new MembershipFeeRequest
        {
            FeeType = "AnnualFee",
            Description = "Annual membership",
            Amount = 12000m,
            CurrencyCode = "ZAR",
            IsProrationEnabled = false,
            IsActive = true,
        });
        Assert.Equal("AnnualFee", fee.FeeType);

        var application = await PostJsonAsync<MembershipApplicationResponse>("/api/v1/membership-applications",
            new CreateMembershipApplicationRequest
            {
                PersonId = personId,
                CategoryId = category.Id,
                SponsorName = "Sponsor",
                Notes = "Onboarding",
                RequiredDocuments = "ID",
            });
        Assert.Equal("Draft", application.Status);

        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/submit");
        Assert.Equal("Submitted", application.Status);
        Assert.NotNull(application.SubmittedAtUtc);

        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/start-review");
        Assert.Equal("UnderReview", application.Status);

        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/approve");
        Assert.Equal("Approved", application.Status);
        Assert.NotNull(application.MemberId);
        Assert.NotNull(application.MemberNumber);
        Assert.StartsWith("MEM-", application.MemberNumber, StringComparison.Ordinal);
        Assert.NotNull(application.ApprovedByUserId);

        // Member registry + directory.
        var search = await GetJsonAsync<MemberSearchResponse>($"/api/v1/members?query={application.MemberNumber}");
        var memberRow = Assert.Single(search.Items);
        Assert.Equal(application.MemberId, memberRow.Id);

        var detail = await GetJsonAsync<MemberDetailResponse>($"/api/v1/members/{application.MemberId}");
        Assert.Equal("Prospect", detail.Status);
        Assert.Equal(personId, detail.PersonId);

        var memberships = await GetJsonAsync<MembershipResponse[]>($"/api/v1/members/{application.MemberId}/memberships");
        var pending = Assert.Single(memberships);
        Assert.Equal("Pending", pending.Status);

        // Activation.
        var activated = await TransitionAsync<MembershipResponse>($"/api/v1/memberships/{pending.Id}/activate");
        Assert.Equal("Active", activated.Status);
        Assert.NotNull(activated.EndDateUtc);

        var activatedDetail = await GetJsonAsync<MemberDetailResponse>($"/api/v1/members/{application.MemberId}");
        Assert.Equal("Active", activatedDetail.Status);

        // Suspension and reinstatement.
        var suspended = await TransitionAsync<MembershipResponse>(
            $"/api/v1/memberships/{pending.Id}/suspend", new MembershipTransitionRequest { Reason = "Disciplinary hold" });
        Assert.Equal("Suspended", suspended.Status);

        var reinstated = await TransitionAsync<MembershipResponse>(
            $"/api/v1/memberships/{pending.Id}/reinstate", new MembershipTransitionRequest { Reason = "Hold lifted" });
        Assert.Equal("Active", reinstated.Status);

        // Dependency: dependant person assigned to the member.
        var dependant = await CreatePersonAsync($"Grace {tag}", $"grace-{tag}@clubstack.test");
        var added = await PostJsonAsync<MembershipDependantResponse>(
            $"/api/v1/members/{application.MemberId}/dependants",
            new AddDependantRequest { DependantPersonId = dependant.Person.Id, RelationType = "Child", Notes = "Minor" });
        Assert.Equal("Child", added.RelationType);

        // Household owns the member as primary.
        var household = await PostJsonAsync<HouseholdResponse>("/api/v1/households",
            new HouseholdRequest { Name = "The Household", PrimaryMemberId = application.MemberId });
        Assert.Contains(household.Members, m => m.PersonId == personId && m.Role == "Primary");

        // Fee assessment (full amount: assessment at period start).
        var assessed = await PostJsonAsync<MembershipFeeAssessmentResponse>("/api/v1/membership-fee-assessments",
            new AssessFeeRequest { MembershipId = pending.Id, FeeType = "AnnualFee", AssessmentDateUtc = null });
        Assert.Equal(12000m, assessed.Amount);
        Assert.False(assessed.IsProrated);
        Assert.Equal("Outstanding", assessed.Status);

        var adjusted = await TransitionAsync<MembershipFeeAssessmentResponse>(
            $"/api/v1/membership-fee-assessments/{assessed.Id}/adjust",
            new FeeAdjustmentRequest { Amount = 6000m, Reason = "Goodwill" });
        Assert.Equal(6000m, adjusted.Amount);

        var voided = await TransitionAsync<MembershipFeeAssessmentResponse>(
            $"/api/v1/membership-fee-assessments/{assessed.Id}/void",
            new FeeVoidRequest { Reason = "Assessed in error" });
        Assert.Equal("Voided", voided.Status);

        // Audit trail recorded for the sensitive approval command.
        await using (var context = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ClubStackDbContext>())
        {
            var audit = await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Membership.Application.Approve" && a.EntityId == application.Id.ToString());
            Assert.NotNull(audit);
            Assert.Equal("MembershipApplication", audit!.EntityType);
            Assert.Contains("MEM-", audit.Reference, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Invalid_transitions_and_duplicate_configuration_return_conflict()
    {
        await LoginAsync();
        var tag = Guid.NewGuid().ToString("N");
        var category = await CreateCategoryAsync($"H{tag[..8]}".ToUpperInvariant(), "Silver");
        var person = await CreatePersonAsync($"Bob {tag}", $"bob-{tag}@clubstack.test");

        // Duplicate category code conflicts.
        var duplicateCategory = await _client.PostAsJsonAsync("/api/v1/membership-categories", new
        {
            code = category.Code,
            name = "Other",
            renewalPeriodMonths = 12,
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateCategory.StatusCode);

        var application = await PostJsonAsync<MembershipApplicationResponse>("/api/v1/membership-applications",
            new CreateMembershipApplicationRequest { PersonId = person.Person.Id, CategoryId = category.Id });
        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/submit");
        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/start-review");
        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/approve");

        var memberships = await GetJsonAsync<MembershipResponse[]>($"/api/v1/members/{application.MemberId}/memberships");
        var membership = Assert.Single(memberships);
        await TransitionAsync<MembershipResponse>($"/api/v1/memberships/{membership.Id}/activate");
        await TransitionAsync<MembershipResponse>($"/api/v1/memberships/{membership.Id}/suspend", new MembershipTransitionRequest { Reason = "Hold" });

        // Suspending an already-suspended membership is an invalid transition.
        var secondSuspend = await _client.PostAsJsonAsync($"/api/v1/memberships/{membership.Id}/suspend",
            new MembershipTransitionRequest { Reason = "Again" });
        Assert.Equal(HttpStatusCode.Conflict, secondSuspend.StatusCode);

        // A rejected application cannot be submitted again.
        var secondApp = await PostJsonAsync<MembershipApplicationResponse>("/api/v1/membership-applications",
            new CreateMembershipApplicationRequest
            {
                PersonId = (await CreatePersonAsync($"Cora {tag}", $"cora-{tag}@clubstack.test")).Person.Id,
                CategoryId = category.Id,
            });
        secondApp = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{secondApp.Id}/submit");
        secondApp = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{secondApp.Id}/reject",
            new RejectMembershipApplicationRequest { Reason = "Ineligible" });
        Assert.Equal("Rejected", secondApp.Status);

        var resubmit = await _client.PostAsync($"/api/v1/membership-applications/{secondApp.Id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, resubmit.StatusCode);
    }

    [Fact]
    public async Task Validation_and_not_found_produce_contractual_statuses()
    {
        await LoginAsync();
        // Invalid application status filter -> 400 problem.
        var invalidFilter = await _client.GetAsync("/api/v1/membership-applications?status=Bogus");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);

        var invalidMemberStatus = await _client.GetAsync("/api/v1/members?status=Wizard");
        Assert.Equal(HttpStatusCode.BadRequest, invalidMemberStatus.StatusCode);

        // Unknown entity ids -> 404.
        var missing = Guid.Empty.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/members/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/membership-applications/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/memberships/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/persons/{missing}")).StatusCode);

        // Fee assessment requires a configured fee for the category + type.
        var category = await CreateCategoryAsync($"V{Guid.NewGuid():N}"[..10].ToUpperInvariant(), "Trial");
        var person = await CreatePersonAsync($"Dax {Guid.NewGuid():N}", $"dax-{Guid.NewGuid():N}@clubstack.test");
        var application = await PostJsonAsync<MembershipApplicationResponse>("/api/v1/membership-applications",
            new CreateMembershipApplicationRequest { PersonId = person.Person.Id, CategoryId = category.Id });
        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/submit");
        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/start-review");
        application = await TransitionAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/approve");
        var memberships = await GetJsonAsync<MembershipResponse[]>($"/api/v1/members/{application.MemberId}/memberships");
        await TransitionAsync<MembershipResponse>($"/api/v1/memberships/{Assert.Single(memberships).Id}/activate");

        var unconfigured = await _client.PostAsJsonAsync("/api/v1/membership-fee-assessments",
            new AssessFeeRequest { MembershipId = Assert.Single(memberships).Id, FeeType = "AnnualFee" });
        Assert.Equal(HttpStatusCode.BadRequest, unconfigured.StatusCode);

        // Invalid enum value on a configured endpoint -> 400 problem with field detail.
        var invalidFeeType = await _client.PostAsJsonAsync("/api/v1/membership-categories/{id}/fees"
                .Replace("{id}", category.Id.ToString()),
            new MembershipFeeRequest { FeeType = "LifetimeFee", Amount = 100m, CurrencyCode = "ZAR" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidFeeType.StatusCode);
    }

    // ---- helpers ----

    private async Task LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { userName = UserName, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<MembershipCategoryResponse> CreateCategoryAsync(string code, string name)
    {
        var response = await PostJsonAsync<MembershipCategoryResponse>("/api/v1/membership-categories",
            new MembershipCategoryRequest
            {
                Code = code,
                Name = name,
                Description = "HTTP test category",
                RenewalPeriodMonths = 12,
                IsActive = true,
            });
        return response;
    }

    private async Task<PersonCreatedResponse> CreatePersonAsync(string name, string email)
    {
        var response = await PostJsonAsync<PersonCreatedResponse>("/api/v1/persons", new PersonRequest
        {
            LegalName = name,
            Email = email,
            Phone = $"07{Guid.NewGuid():N}"[..10],
        });
        return response;
    }

    private async Task<MembershipFeeResponse> CreateFeeAsync(Guid categoryId, MembershipFeeRequest request)
        => await PostJsonAsync<MembershipFeeResponse>($"/api/v1/membership-categories/{categoryId}/fees", request);

    private async Task<T> PostJsonAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(_json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> TransitionAsync<T>(string url, object? body = null)
    {
        var response = body is null
            ? await _client.PostAsync(url, null)
            : await _client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(_json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> GetJsonAsync<T>(string url)
        => await _client.GetFromJsonAsync<T>(url, _json) ?? throw new InvalidOperationException("Empty response body.");
}