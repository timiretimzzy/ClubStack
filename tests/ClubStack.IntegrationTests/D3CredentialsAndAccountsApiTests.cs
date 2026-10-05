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
/// HTTP-level verification of the D3 surface through the real API pipeline: the
/// Phase 5 §31/§32 credential workflow (issue/activate/suspend/reactivate/resolve)
/// and the Phase 5 §33/§34 member account ledger, including permission gating,
/// Problem Details status codes and the audit trail.
/// </summary>
public sealed class D3CredentialsAndAccountsApiTests : IAsyncLifetime
{
    private const string AdminUserName = "d3-admin";
    private const string AdminPassword = "Str0ng!Pass12";
    private const string LimitedUserName = "d3-reception";
    private const string LimitedPassword = "Str0ng!Pass12";

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _limitedClient = null!;

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

        _limitedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
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
                AdminUserName, $"{AdminUserName}@example.com", "D3", "Admin", "0000000000",
                AdminPassword, ["System Administrator"]));
            await identity.CreateUserAsync(new CreateUserInput(
                LimitedUserName, $"{LimitedUserName}@example.com", "D3", "Reception", "0000000000",
                LimitedPassword, ["Reception Officer"]));
        }
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _client.Dispose();
        _limitedClient.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task Anonymous_requests_to_d3_endpoints_are_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/credentials")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/v1/member-accounts/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/v1/members/{Guid.NewGuid()}/account")).StatusCode);
    }

    [Fact]
    public async Task Credential_and_account_flows_run_over_http_with_audit_trail()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var tag = Guid.NewGuid().ToString("N");

        var memberId = await CreateActiveMemberAsync(tag);

        // ACC-001: the member account resolves through the Member 360 resource.
        var account = await GetJsonAsync<MemberAccountResponse>($"/api/v1/members/{memberId}/account", _client);
        Assert.StartsWith("ACC", account.AccountNumber, StringComparison.Ordinal);
        Assert.Equal("Active", account.Status);
        Assert.Equal("ZAR", account.CurrencyCode);
        Assert.Equal(0m, account.Balance.AmountDue);

        // Issue + activate + resolve (CRED-001..005, Phase 5 §31/§32).
        var cardValue = $"HTTPCARD{tag[..6]}".ToUpperInvariant();
        var issued = await PostJsonAsync<CredentialResponse>("/api/v1/credentials", new IssueCredentialRequest
        {
            CredentialType = "MagneticStripe",
            CredentialValue = cardValue,
        }, _client);
        Assert.Equal("Issued", issued.Status);
        Assert.StartsWith("CRD", issued.CredentialNumber, StringComparison.Ordinal);

        var activated = await PostJsonAsync<CredentialResponse>($"/api/v1/credentials/{issued.Id}/activate",
            new ActivateCredentialRequest { MemberId = memberId }, _client);
        Assert.Equal("Active", activated.Status);
        Assert.Equal(memberId, activated.MemberId);

        var resolved = await PostJsonAsync<ResolveCredentialResponse>("/api/v1/credentials/resolve",
            new ResolveCredentialRequest { CredentialValue = cardValue.ToLowerInvariant() }, _client);
        Assert.Equal(activated.Id, resolved.CredentialId);
        Assert.Equal(memberId, resolved.MemberId);
        Assert.Equal("Active", resolved.MemberStatus);

        // Suspend blocks resolution; reactivate restores it.
        var suspended = await PostJsonAsync<CredentialResponse>($"/api/v1/credentials/{issued.Id}/suspend",
            new CredentialTransitionRequest { Reason = "Temporary hold" }, _client);
        Assert.Equal("Suspended", suspended.Status);

        var blocked = await _client.PostAsJsonAsync("/api/v1/credentials/resolve", new ResolveCredentialRequest { CredentialValue = cardValue });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var reactivated = await PostJsonAsync<CredentialResponse>($"/api/v1/credentials/{issued.Id}/reactivate",
            new CredentialTransitionRequest { Reason = "Hold lifted" }, _client);
        Assert.Equal("Active", reactivated.Status);

        // History is immutable and complete.
        var history = await GetJsonAsync<CredentialHistoryResponse[]>($"/api/v1/credentials/{issued.Id}/history", _client);
        Assert.Contains(history, h => h.NewState == "Suspended" && h.Reason.Contains("Temporary"));
        Assert.Contains(history, h => h.NewState == "Active" && h.Reason.Contains("Hold lifted"));

        // Member account ledger (Phase 5 §33/§34): charge, payment, balance.
        var charge = await PostJsonAsync<AccountTransactionResponse>($"/api/v1/member-accounts/{account.Id}/charges",
            new ChargeRequest { Description = "Dining", Amount = 750m, CurrencyCode = "ZAR" }, _client);
        Assert.Equal("Charge", charge.TransactionType);
        Assert.Equal(750m, charge.Amount);
        Assert.True(charge.IsDebit);

        var payment = await PostJsonAsync<AccountTransactionResponse>($"/api/v1/member-accounts/{account.Id}/payments",
            new PaymentRequest
            {
                Description = "Cash payment",
                Amount = 250m,
                CurrencyCode = "ZAR",
                PaymentMethodCode = "CASH",
                ExternalReference = $"pay-{tag}",
            }, _client);
        Assert.Equal("Payment", payment.TransactionType);
        Assert.Equal("CASH", payment.PaymentMethodCode);

        var balance = await GetJsonAsync<AccountBalanceResponse>($"/api/v1/member-accounts/{account.Id}/balance", _client);
        Assert.Equal(500m, balance.AmountDue);

        var transactions = await GetJsonAsync<AccountTransactionSearchResponse>(
            $"/api/v1/member-accounts/{account.Id}/transactions", _client);
        Assert.Equal(2, transactions.TotalCount);

        var statement = await PostJsonAsync<StatementResponse>($"/api/v1/member-accounts/{account.Id}/statement",
            new { }, _client);
        Assert.StartsWith("STMT", statement.StatementNumber, StringComparison.Ordinal);
        Assert.Equal(0m, statement.OpeningBalance);
        Assert.Equal(500m, statement.ClosingBalance);
        Assert.Equal(2, statement.Transactions.Count);

        var aging = await GetJsonAsync<AgingResponse>($"/api/v1/member-accounts/{account.Id}/aging", _client);
        Assert.Equal(500m, aging.TotalOutstanding);

        // Sensitive commands are audited.
        await using (var context = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ClubStackDbContext>())
        {
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Credentials.Credential.Issue" && a.EntityId == issued.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Credentials.Credential.Activate" && a.EntityId == issued.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Accounts.Payment.Record" && a.EntityId == payment.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Accounts.Transaction.Charge" && a.EntityId == charge.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Accounts.Statement.Generate"));
        }
    }

    [Fact]
    public async Task Permission_missing_roles_cannot_issue_cards_or_view_accounts()
    {
        await LoginAsync(_limitedClient, LimitedUserName, LimitedPassword);

        // Reception Officer has CredentialsCardView but not Issue, and no account access.
        var issue = await _limitedClient.PostAsJsonAsync("/api/v1/credentials", new IssueCredentialRequest
        {
            CredentialType = "MagneticStripe",
            CredentialValue = "FORBIDDEN-1",
        });
        Assert.Equal(HttpStatusCode.Forbidden, issue.StatusCode);

        var account = await _limitedClient.GetAsync($"/api/v1/member-accounts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, account.StatusCode);

        // View-only card permissions still work: resolving an unknown card gives 404, not 403.
        var resolve = await _limitedClient.PostAsJsonAsync("/api/v1/credentials/resolve", new ResolveCredentialRequest { CredentialValue = "CARDS-VIEW" });
        Assert.Equal(HttpStatusCode.NotFound, resolve.StatusCode);
    }

    // ---- helpers ----

    private async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> CreateActiveMemberAsync(string tag)
    {
        var category = await PostJsonAsync<MembershipCategoryResponse>("/api/v1/membership-categories",
            new MembershipCategoryRequest
            {
                Code = $"D3{tag[..6]}".ToUpperInvariant(),
                Name = "HTTP category",
                Description = "E2E category",
                RenewalPeriodMonths = 12,
                IsActive = true,
            }, _client);
        var person = await PostJsonAsync<PersonCreatedResponse>("/api/v1/persons", new PersonRequest
        {
            LegalName = $"E2E {tag}",
            Email = $"e2e-{tag}@clubstack.test",
            Phone = $"07{Guid.NewGuid():N}"[..10],
        }, _client);

        var application = await PostJsonAsync<MembershipApplicationResponse>("/api/v1/membership-applications",
            new CreateMembershipApplicationRequest { PersonId = person.Person.Id, CategoryId = category.Id }, _client);
        application = await PostJsonAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/submit", new { }, _client);
        application = await PostJsonAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/start-review", new { }, _client);
        application = await PostJsonAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/approve", new { }, _client);

        var memberships = await GetJsonAsync<MembershipResponse[]>($"/api/v1/members/{application.MemberId}/memberships", _client);
        await PostJsonAsync<MembershipResponse>($"/api/v1/memberships/{Assert.Single(memberships).Id}/activate", new { }, _client);
        return application.MemberId!.Value;
    }

    private async Task<T> PostJsonAsync<T>(string url, object body, HttpClient client)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(_json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> GetJsonAsync<T>(string url, HttpClient client)
        => await client.GetFromJsonAsync<T>(url, _json) ?? throw new InvalidOperationException("Empty response body.");
}