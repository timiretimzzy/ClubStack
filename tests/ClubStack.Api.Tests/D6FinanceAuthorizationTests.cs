using System.Net;
using ClubStack.Api.Authorization;
using ClubStack.Api.Controllers;
using ClubStack.Application.Authorization;
using ClubStack.Application.Auditing;
using ClubStack.Application.Correlation;
using ClubStack.Application.Finance;
using ClubStack.Domain.Common;
using ClubStack.Domain.Finance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ISystemClock = ClubStack.Domain.Common.ISystemClock;

namespace ClubStack.Api.Tests;

/// <summary>
/// D6 finance authorization (PM Decision Pack §13).
///
/// Every finance route is driven through the real ASP.NET Core authentication and
/// authorization pipeline using the application's own <c>Permission:*</c> policies,
/// so a change to a policy or to an action's <c>[Authorize]</c> attribute is caught
/// here rather than in production.
///
/// Scope, stated plainly: the harness substitutes stub finance services and a
/// header-driven authentication handler. It therefore proves the <em>authorization
/// decision</em> for every route — 401 when anonymous, 403 when the specific
/// permission is absent, and a real response when it is present. Finance behaviour
/// itself is covered by the PostgreSQL integration suite.
/// </summary>
public sealed class D6FinanceAuthorizationTests
{
    public const string Prefix = "/api/v1/finance";

    /// <summary>Every D6 finance route paired with the permission it must require.</summary>
    public static TheoryData<string, string, string> Routes => new()
    {
        // Chart of accounts (B2, B3, B7, B17)
        { "GET", "accounts", Permissions.FinanceAccountView },
        { "GET", "accounts/system", Permissions.FinanceAccountView },
        { "GET", "accounts/base-currency", Permissions.FinanceAccountView },
        { "GET", "accounts/11111111-1111-1111-1111-111111111111", Permissions.FinanceAccountView },
        { "POST", "accounts", Permissions.FinanceAccountManage },
        { "PUT", "accounts/11111111-1111-1111-1111-111111111111", Permissions.FinanceAccountManage },
        { "POST", "accounts/11111111-1111-1111-1111-111111111111/deactivate", Permissions.FinanceAccountManage },
        { "PUT", "accounts/base-currency", Permissions.FinanceAccountManage },
        { "GET", "accounts/posting-rules", Permissions.FinanceAccountView },
        { "PUT", "accounts/posting-rules/POS-CASH-SALE/mappings", Permissions.FinanceAccountManage },
        { "DELETE", "accounts/posting-rules/POS-CASH-SALE/mappings/11111111-1111-1111-1111-111111111111", Permissions.FinanceAccountManage },

        // Journals (B1, B4)
        { "GET", "journals", Permissions.FinanceJournalView },
        { "GET", "journals/11111111-1111-1111-1111-111111111111", Permissions.FinanceJournalView },
        { "GET", "journals/11111111-1111-1111-1111-111111111111/validation", Permissions.FinanceJournalView },
        { "POST", "journals", Permissions.FinanceJournalCreate },
        { "POST", "journals/11111111-1111-1111-1111-111111111111/post", Permissions.FinanceJournalPost },
        { "POST", "journals/11111111-1111-1111-1111-111111111111/cancel", Permissions.FinanceJournalPost },
        { "POST", "journals/11111111-1111-1111-1111-111111111111/reverse", Permissions.FinanceJournalReverse },

        // Periods (B5)
        { "GET", "periods", Permissions.FinancePeriodView },
        { "GET", "periods/11111111-1111-1111-1111-111111111111", Permissions.FinancePeriodView },
        { "GET", "periods/current", Permissions.FinancePeriodView },
        { "POST", "periods", Permissions.FinancePeriodOpen },
        { "POST", "periods/11111111-1111-1111-1111-111111111111/close", Permissions.FinancePeriodClose },
        { "POST", "periods/11111111-1111-1111-1111-111111111111/reopen", Permissions.FinancePeriodReopen },

        // Receivables (B13)
        { "GET", "receivables", Permissions.FinanceArView },
        { "GET", "receivables/11111111-1111-1111-1111-111111111111", Permissions.FinanceArView },
        { "POST", "receivables", Permissions.FinanceArManage },
        { "POST", "receivables/11111111-1111-1111-1111-111111111111/cancel", Permissions.FinanceArManage },

        // Payables (B13)
        { "GET", "payables", Permissions.FinanceApView },
        { "GET", "payables/11111111-1111-1111-1111-111111111111", Permissions.FinanceApView },
        { "POST", "payables", Permissions.FinanceApManage },
        { "POST", "payables/11111111-1111-1111-1111-111111111111/cancel", Permissions.FinanceApManage },

        // Payments (B14)
        { "GET", "payments", Permissions.FinancePaymentManage },
        { "GET", "payments/11111111-1111-1111-1111-111111111111", Permissions.FinancePaymentManage },
        { "GET", "payments/by-idempotency-key/some-key", Permissions.FinancePaymentManage },
        { "POST", "payments", Permissions.FinancePaymentManage },

        // Reconciliation (B15)
        { "GET", "reconciliations", Permissions.FinanceReconciliationPerform },
        { "GET", "reconciliations/11111111-1111-1111-1111-111111111111", Permissions.FinanceReconciliationPerform },
        { "POST", "reconciliations", Permissions.FinanceReconciliationPerform },
        { "POST", "reconciliations/11111111-1111-1111-1111-111111111111/matches", Permissions.FinanceReconciliationPerform },
        { "DELETE", "reconciliations/11111111-1111-1111-1111-111111111111/matches/11111111-1111-1111-1111-111111111111", Permissions.FinanceReconciliationPerform },
        { "POST", "reconciliations/11111111-1111-1111-1111-111111111111/complete", Permissions.FinanceReconciliationPerform },
        { "POST", "reconciliations/11111111-1111-1111-1111-111111111111/cancel", Permissions.FinanceReconciliationPerform },

        // Reports (B16)
        { "GET", "reports/overview", Permissions.FinanceReportView },
        { "GET", "reports/trial-balance", Permissions.FinanceReportView },
        { "GET", "reports/general-ledger/11111111-1111-1111-1111-111111111111", Permissions.FinanceReportView },
        { "GET", "reports/receivable-aging", Permissions.FinanceReportView },
        { "GET", "reports/payable-aging", Permissions.FinanceReportView },
        { "GET", "reports/cash-position", Permissions.FinanceReportView },
        { "GET", "reports/periods", Permissions.FinanceReportView },
    };

    public static TheoryData<string, string> RouteMethodsAndPaths
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var row in Routes)
            {
                data.Add((string)row[0], (string)row[1]);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RouteMethodsAndPaths))]
    public async Task Anonymous_request_is_rejected_with_401(string method, string path)
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Authenticated_user_without_the_permission_is_rejected_with_403(
        string method, string path, string permission)
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        // Hold every *other* finance permission and withhold only this route's, so a 403
        // proves the route is gated on that specific permission rather than on being an
        // authenticated finance user.
        var withheld = string.Join(',', FinanceApiFactory.FinancePermissions.All
            .Where(p => !string.Equals(p, permission, StringComparison.Ordinal)));

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, withheld);

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Permission_holder_reaches_the_action(string method, string path, string permission)
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, permission);

        using var response = await SendAsync(client, method, path);

        // Read routes answer from the stubs with 200. A command route that supplies no
        // body is rejected by model validation, which still proves the gate opened: the
        // only statuses that would indicate a broken gate are 401 and 403.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Separation of duty: the sensitive finance actions are not interchangeable ----

    [Fact]
    public async Task Journal_view_permission_cannot_post_a_journal()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinanceJournalView);

        using var response = await client.PostAsync(
            $"{Prefix}/journals/11111111-1111-1111-1111-111111111111/post",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Journal_post_permission_cannot_reverse_a_journal()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinanceJournalPost);

        using var response = await client.PostAsync(
            $"{Prefix}/journals/11111111-1111-1111-1111-111111111111/reverse",
            content: new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Period_close_permission_cannot_reopen_a_period()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinancePeriodClose);

        using var response = await client.PostAsync(
            $"{Prefix}/periods/11111111-1111-1111-1111-111111111111/reopen",
            content: new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receivable_view_permission_cannot_manage_receivables()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinanceArView);

        using var response = await client.PostAsync($"{Prefix}/receivables", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receivable_manage_permission_cannot_touch_payables()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinanceArManage);

        using var response = await client.GetAsync($"{Prefix}/payables");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Payment_permission_cannot_manage_the_chart_of_accounts()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinancePaymentManage);

        using var response = await client.PostAsync($"{Prefix}/accounts", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Report_view_permission_cannot_open_a_financial_period()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FinanceReportView);

        using var response = await client.PostAsync($"{Prefix}/periods", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_cashier_cannot_reach_any_finance_route()
    {
        using var factory = FinanceApiFactory.Create();
        using var client = factory.CreateClient();

        var cashierPermissions = string.Join(',', new[]
        {
            Permissions.CashieringSessionOpen,
            Permissions.PosSaleCreate,
            Permissions.PosSaleComplete,
            Permissions.FiscalizationDeviceView,
            Permissions.FiscalizationDayOpen,
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, cashierPermissions);

        foreach (var row in Routes)
        {
            using var response = await SendAsync(client, (string)row[0], (string)row[1]);
            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"{(string)row[0]} {(string)row[1]} answered {response.StatusCode} to a cashier.");
        }
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"{Prefix}/{path}");

        // Command routes bind a body; send a valid empty JSON object so model validation,
        // not deserialization, is what an authorized-but-empty request trips over.
        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
    }
}

/// <summary>
/// Serves the real D6 finance controllers under the real permission policies, with
/// stub finance services and a header-driven authentication handler.
/// </summary>
internal static class FinanceApiFactory
{
    public static TestServer Create()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(FinancialAccountsController).Assembly);

        // The application's own policy definitions, so a policy change is not duplicated.
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in FinancePermissions.All)
            {
                options.AddPermissionPolicy(permission);
            }
        });

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddSingleton<IAuditRecorder, StubAuditRecorder>();
        builder.Services.AddScoped<ICorrelationContext, CorrelationContextV1>();
        builder.Services.AddSingleton<ISystemClock, StubSystemClock>();

        builder.Services.AddSingleton<IFinancialAccountService, StubFinancialAccountService>();
        builder.Services.AddSingleton<IJournalService, StubJournalService>();
        builder.Services.AddSingleton<IFinancialPeriodService, StubFinancialPeriodService>();
        builder.Services.AddSingleton<IReceivableService, StubReceivableService>();
        builder.Services.AddSingleton<IPayableService, StubPayableService>();
        builder.Services.AddSingleton<IFinancePaymentService, StubFinancePaymentService>();
        builder.Services.AddSingleton<IFinancialReconciliationService, StubReconciliationService>();
        builder.Services.AddSingleton<IFinanceReportService, StubReportService>();

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.StartAsync().GetAwaiter().GetResult();
        return app.GetTestServer();
    }

    /// <summary>Every D6 finance permission, for policy registration.</summary>
    internal static class FinancePermissions
    {
        public static readonly string[] All =
        [
            Permissions.FinanceAccountView,
            Permissions.FinanceAccountManage,
            Permissions.FinanceJournalView,
            Permissions.FinanceJournalCreate,
            Permissions.FinanceJournalPost,
            Permissions.FinanceJournalReverse,
            Permissions.FinancePeriodView,
            Permissions.FinancePeriodOpen,
            Permissions.FinancePeriodClose,
            Permissions.FinancePeriodReopen,
            Permissions.FinanceArView,
            Permissions.FinanceArManage,
            Permissions.FinanceApView,
            Permissions.FinanceApManage,
            Permissions.FinancePaymentManage,
            Permissions.FinanceReconciliationPerform,
            Permissions.FinanceReportView,
        ];
    }
}
