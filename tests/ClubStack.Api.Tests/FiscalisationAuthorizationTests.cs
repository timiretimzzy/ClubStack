using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ClubStack.Api.Authorization;
using ClubStack.Api.Controllers;
using ClubStack.Application.Authorization;
using ClubStack.Application.Fiscalization;
using ClubStack.Hardware.Fiscalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClubStack.Api.Tests;

/// <summary>
/// D5.5 administration authorization (Part 9).
///
/// These tests drive the <em>real</em> <see cref="FiscalisationController"/> through the
/// real ASP.NET Core authentication and authorization pipeline, using the application's
/// own <c>Permission:*</c> policy definitions. Nothing about the permission model is
/// re-implemented here, so a change to a policy or to an action's
/// <c>[Authorize]</c> attribute is caught.
///
/// Scope, stated plainly: this harness substitutes a stub fiscalisation service and a
/// test authentication handler. It therefore proves the <em>authorization decision</em>
/// for every route — 401 when anonymous, 403 when the permission is absent, and reaching
/// the action when it is present. It does not exercise server-side session enforcement or
/// the database, which are covered by the integration suite instead.
/// </summary>
public sealed class FiscalisationAuthorizationTests
{
    public const string RoutePrefix = "/api/v1/fiscalisation";

    /// <summary>Every fiscalisation route paired with the permission it must require.</summary>
    public static TheoryData<string, string, string> Routes => new()
    {
        { "GET", "device/status", Permissions.FiscalizationDeviceView },
        { "GET", "device/configuration", Permissions.FiscalizationDeviceView },
        { "POST", "device/provision", Permissions.FiscalizationDeviceManage },
        { "GET", "day", Permissions.FiscalizationDeviceView },
        { "POST", "day/open", Permissions.FiscalizationDayOpen },
        { "POST", "day/close", Permissions.FiscalizationDayClose },
        { "GET", "documents", Permissions.FiscalizationTransactionView },
        { "GET", "documents/by-sale/11111111-1111-1111-1111-111111111111", Permissions.FiscalizationTransactionView },
        { "POST", "documents/11111111-1111-1111-1111-111111111111/retry", Permissions.FiscalizationTransactionRetry },
        { "POST", "documents/submit-pending", Permissions.FiscalizationTransactionRetry },
    };

    /// <summary>Just the method and path of every route, for cases that need no permission.</summary>
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

    // ---- Anonymous ----

    [Theory]
    [MemberData(nameof(RouteMethodsAndPaths))]
    public async Task Anonymous_request_is_rejected_with_401(string method, string path)
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Authenticated but not permitted ----

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Authenticated_user_without_the_permission_is_rejected_with_403(
        string method, string path, string permission)
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        // Hold every *other* fiscalisation permission and withhold only the one this route
        // requires. A 403 here therefore proves the route is gated on that specific
        // permission rather than merely on being a signed-in fiscal operator.
        var withheld = string.Join(',', FiscalisationApiFactory.FiscalPermissions
            .All.Where(p => !string.Equals(p, permission, StringComparison.Ordinal)));

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, withheld);

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Administrator_holding_the_permission_reaches_the_action(
        string method, string path, string permission)
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, permission);

        using var response = await SendAsync(client, method, path);

        // The stub service answers every call, so anything other than 401/403 proves the
        // authorization decision allowed the request through to the action.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- A permission does not unlock a route it was not granted for ----

    [Fact]
    public async Task Device_manage_permission_does_not_open_the_transaction_routes()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationDeviceManage);

        using var response = await client.GetAsync($"{RoutePrefix}/documents");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Transaction_view_permission_does_not_open_device_management()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationTransactionView);

        using var response = await client.PostAsJsonAsync(
            $"{RoutePrefix}/device/license",
            new { licenseKey = "not-a-real-key" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Device_view_permission_cannot_reconfigure_the_device()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationDeviceView);

        using var response = await client.PostAsJsonAsync(
            $"{RoutePrefix}/device/configuration",
            new
            {
                deviceId = "18084",
                serialNumber = "FSTK-00000001",
                companyName = "Test",
                branchName = "HQ",
                city = "Harare",
                activationKey = "not-a-real-key",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Transaction_view_permission_cannot_retry_a_document()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationTransactionView);

        using var response = await client.PostAsync(
            $"{RoutePrefix}/documents/11111111-1111-1111-1111-111111111111/retry",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Day_open_permission_cannot_close_the_day()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationDayOpen);

        using var response = await client.PostAsync($"{RoutePrefix}/day/close", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A cashier holds Device.View (to see whether the till can sell) and the day and
    /// transaction permissions. Provisioning re-keys a device and installs a license, so
    /// it must stay outside a cashier's reach even though every other fiscalisation read
    /// is available to them.
    /// </summary>
    [Fact]
    public async Task A_cashier_cannot_provision_a_device()
    {
        var cashierPermissions = new[]
        {
            Permissions.CashieringSessionOpen,
            Permissions.FiscalizationDeviceView,
            Permissions.FiscalizationDayOpen,
            Permissions.FiscalizationDayClose,
            Permissions.FiscalizationTransactionView,
            Permissions.FiscalizationTransactionRetry,
        };

        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        foreach (var permission in cashierPermissions)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, permission);
        }

        using var response = await client.PostAsJsonAsync(
            $"{RoutePrefix}/device/provision",
            new
            {
                deviceId = "38529",
                serialNumber = "HOYOTEST",
                environment = "Test",
                activationKey = "not-a-real-key",
                licenseKey = "not-a-real-license",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Device_view_permission_cannot_provision_a_device()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationDeviceView);

        using var response = await client.PostAsJsonAsync(
            $"{RoutePrefix}/device/provision",
            new
            {
                deviceId = "38529",
                serialNumber = "HOYOTEST",
                environment = "Test",
                activationKey = "not-a-real-key",
                licenseKey = "not-a-real-license",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// An unknown environment must be rejected before the engine is touched, so a typo
    /// cannot silently resolve to a default and re-key a device against the wrong FDMS.
    /// </summary>
    [Fact]
    public async Task Provisioning_rejects_an_unknown_environment()
    {
        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationDeviceManage);

        using var response = await client.PostAsJsonAsync(
            $"{RoutePrefix}/device/provision",
            new
            {
                deviceId = "38529",
                serialNumber = "HOYOTEST",
                environment = "Sandbox",
                activationKey = "not-a-real-key",
                licenseKey = "not-a-real-license",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The provisioning response reports the registration outcome but must not echo
    /// either write-only key back to the caller.
    /// </summary>
    [Fact]
    public async Task Provisioning_response_never_echoes_the_activation_or_license_key()
    {
        const string activationKey = "activation-key-must-not-be-returned";
        const string licenseKey = "license-key-must-not-be-returned";

        using var factory = FiscalisationApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.FiscalizationDeviceManage);

        using var response = await client.PostAsJsonAsync(
            $"{RoutePrefix}/device/provision",
            new
            {
                deviceId = "38529",
                serialNumber = "HOYOTEST",
                environment = "Test",
                activationKey,
                licenseKey,
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(activationKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain(licenseKey, body, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
        => method switch
        {
            "GET" => client.GetAsync(RoutePrefix + "/" + path),
            _ => client.PostAsync(RoutePrefix + "/" + path, content: null),
        };
}

/// <summary>
/// Builds a host that serves the real controller under the real permission policies,
/// with a stub fiscalisation service and a header-driven authentication handler.
/// </summary>
internal static class FiscalisationApiFactory
{
    public static TestServer Create()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(FiscalisationController).Assembly);

        // The application's own policy definitions, so a policy change is not duplicated.
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in FiscalPermissions.All)
            {
                options.AddPermissionPolicy(permission);
            }
        });
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddSingleton<IFiscalisationService, StubFiscalisationService>();
        builder.Services.AddSingleton<StubFiscalisationService>();

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.StartAsync().GetAwaiter().GetResult();
        return app.GetTestServer();
    }

    /// <summary>Every D5.5 fiscalisation permission, for policy registration.</summary>
    internal static class FiscalPermissions
    {
        public static readonly string[] All =
        [
            Permissions.FiscalizationDeviceView,
            Permissions.FiscalizationDeviceManage,
            Permissions.FiscalizationDayOpen,
            Permissions.FiscalizationDayClose,
            Permissions.FiscalizationTransactionView,
            Permissions.FiscalizationTransactionRetry,
        ];
    }
}

/// <summary>
/// Authenticates by a request header whose value names the permission claims to grant.
/// No header means anonymous, which is what the 401 cases rely on.
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string HeaderName = "X-Test-Permissions";

    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var granted) || string.IsNullOrWhiteSpace(granted))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Name, "test-user"),
        };

        foreach (var permission in granted.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, permission));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Answers every fiscalisation call so an authorized request produces a real response
/// rather than failing on an unresolved dependency. Authorization is what is under test.
/// </summary>
internal sealed class StubFiscalisationService : IFiscalisationService
{
    private static readonly FiscalDeviceStatusDto Status = new(
        Available: true,
        DeviceId: "18084",
        SerialNumber: "FSTK-00000001",
        CompanyName: "Test Club",
        TaxpayerName: "Test Club",
        TaxpayerTaxNumber: "0000000000",
        VatNumber: "0000000000",
        LicenseActive: true,
        LicenseStatus: "active",
        LicenseStart: null,
        LicenseExpiry: null,
        CertificateCount: 0,
        FiscalDayState: nameof(FiscalDayState.Opened),
        FiscalDayStatus: "Open",
        LastFiscalDayNo: "1",
        LastReceiptGlobalNo: null,
        ZimraReachable: false,
        ZimraSource: null,
        LastError: null);

    private static readonly FiscalDeviceConfigurationDto Configuration = new(
        DeviceId: "18084",
        SerialNumber: "FSTK-00000001",
        CompanyName: "Test Club",
        BranchName: "Head Office",
        City: "Harare",
        Environment: "ONLINE",
        StandardRatedTaxId: 515,
        ZeroRatedTaxId: 1,
        ExemptTaxId: 2,
        CurrencyCode: "ZWG",
        ServerUrl: "https://fdms.zimra.co.zw",
        CertificateFolder: null);

    private static readonly FiscalDayStatusDto Day = new(
        State: nameof(FiscalDayState.Opened),
        FiscalDayStatus: "Open",
        LastFiscalDayNo: "1",
        LastReceiptGlobalNo: null,
        ZimraReachable: false,
        ZimraSource: null);

    public Task<FiscalDeviceStatusDto> GetDeviceStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Status);

    public Task<FiscalDeviceConfigurationDto> GetDeviceConfigurationAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Configuration);

    public Task<FiscalDayStatusDto> GetFiscalDayStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Day);

    public Task<FiscalDayActionResult> OpenFiscalDayAsync(string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDayActionResult(true, "opened", "Fiscal day opened.", "1", "op-1", Day));

    public Task<FiscalDayActionResult> CloseFiscalDayAsync(string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDayActionResult(true, "closed", "Fiscal day closed.", "1", "op-1", Day));

    public Task<FiscalisationDto?> GetForSaleAsync(Guid saleId, CancellationToken cancellationToken = default)
        => Task.FromResult<FiscalisationDto?>(null);

    public Task<FiscalisationSearchResult> SearchAsync(
        FiscalisationSearchInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalisationSearchResult([], 0, input.Page, input.PageSize));

    public Task<FiscalisationRetryResult> RetryAsync(
        Guid id, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalisationRetryResult(true, "ok", "Retried.", null));

    public Task<int> SubmitPendingAsync(string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<FiscalDeviceActionResult> SetLicenseAsync(
        string licenseKey, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDeviceActionResult(true, "applied", "License applied.", Status));

    public Task<FiscalDeviceActionResult> UpdateConfigurationAsync(
        FiscalDeviceConfigurationUpdate update, string actorUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(new FiscalDeviceActionResult(true, "applied", "Configuration applied.", Status));

    /// <summary>Secrets the stub was asked to provision with, so a test can assert they were never returned.</summary>
    public List<string> ProvisioningSecretsSeen { get; } = new();

    public Task<FiscalDeviceProvisioningResult> ProvisionDeviceAsync(
        FiscalDeviceProvisioningRequest request, string actorUserId, CancellationToken cancellationToken = default)
    {
        ProvisioningSecretsSeen.Add(request.ActivationKey);
        ProvisioningSecretsSeen.Add(request.LicenseKey);
        return Task.FromResult(new FiscalDeviceProvisioningResult(
            true,
            "device-provisioned",
            "The device was registered and licensed.",
            request.Environment.ToString(),
            "https://fdmstest.zimra.co.zw",
            true,
            true,
            true,
            true,
            "op-register",
            "Test Club",
            "0000000000",
            1,
            @"C:\FiscalStackZW certificates\38529",
            Status,
            Array.Empty<string>()));
    }
}
