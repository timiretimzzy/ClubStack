using System.Net;
using ClubStack.Api.Authorization;
using ClubStack.Api.Controllers;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Application.Golf;
using ClubStack.Domain.Common;
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
/// D7 golf authorization.
///
/// <para>Every golf route is driven through the real ASP.NET Core authentication and
/// authorization pipeline using the application's own <c>Permission:*</c> policies,
/// so a change to a policy or to an action's <c>[Authorize]</c> attribute is caught
/// here rather than in production. The first nine routes in the table are the exact
/// Phase 8 §11 surface; the rest are the derived configuration and management
/// commands.</para>
///
/// <para>Scope, stated plainly: the harness substitutes stub golf services and a
/// header-driven authentication handler. It therefore proves the <em>authorization
/// decision</em> for every route — 401 when anonymous, 403 when the specific
/// permission is absent, and a real response when it is present. Golf behaviour
/// itself is covered by the PostgreSQL integration suite.</para>
/// </summary>
public sealed class D7GolfAuthorizationTests
{
    public const string Prefix = "/api/v1/golf";

    /// <summary>Every D7 golf route paired with the permission it must require.</summary>
    public static TheoryData<string, string, string> Routes => new()
    {
        // ---- The exact Phase 8 §11 golf surface ----

        // Courses (GOLF-001)
        { "GET", "courses", Permissions.GolfCourseView },

        // Tee sheet and slots (GOLF-002 through GOLF-005)
        { "GET", "tee-sheet", Permissions.GolfTeeSheetView },
        { "GET", "tee-slots", Permissions.GolfTeeSheetView },

        // Bookings (GOLF-006 through GOLF-022)
        { "POST", "bookings", Permissions.GolfBookingCreate },
        { "GET", "bookings/11111111-1111-1111-1111-111111111111", Permissions.GolfBookingView },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/confirm", Permissions.GolfBookingEdit },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/cancel", Permissions.GolfBookingCancel },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/check-in", Permissions.GolfBookingCheckIn },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/no-show", Permissions.GolfBookingEdit },

        // ---- Derived configuration and management commands ----

        { "GET", "courses/11111111-1111-1111-1111-111111111111", Permissions.GolfCourseView },
        { "POST", "courses", Permissions.GolfCourseManage },
        { "POST", "courses/11111111-1111-1111-1111-111111111111/status", Permissions.GolfCourseManage },
        { "POST", "courses/11111111-1111-1111-1111-111111111111/tees", Permissions.GolfCourseManage },
        { "GET", "tees/11111111-1111-1111-1111-111111111111", Permissions.GolfCourseView },
        { "PUT", "tees/11111111-1111-1111-1111-111111111111", Permissions.GolfCourseManage },
        { "POST", "tees/11111111-1111-1111-1111-111111111111/status", Permissions.GolfCourseManage },
        { "GET", "tee-slots/11111111-1111-1111-1111-111111111111", Permissions.GolfTeeSheetView },
        { "POST", "tee-slots", Permissions.GolfTeeSlotManage },
        { "POST", "tee-slots/generate", Permissions.GolfTeeSlotManage },
        { "POST", "tee-slots/11111111-1111-1111-1111-111111111111/status", Permissions.GolfTeeSlotManage },

        { "GET", "bookings", Permissions.GolfBookingView },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/players", Permissions.GolfBookingEdit },
        { "DELETE", "bookings/11111111-1111-1111-1111-111111111111/players/22222222-2222-2222-2222-222222222222", Permissions.GolfBookingEdit },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/complete", Permissions.GolfBookingEdit },

        { "GET", "charges", Permissions.GolfBookingView },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/charges", Permissions.GolfChargeCreate },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/charges/post", Permissions.GolfChargeCreate },
        { "POST", "bookings/11111111-1111-1111-1111-111111111111/charges/22222222-2222-2222-2222-222222222222/waive", Permissions.GolfChargeCreate },

        // Guests (GOLF-008/GOLF-009)
        { "GET", "guests", Permissions.GolfTeeSheetView },
        { "GET", "guests/11111111-1111-1111-1111-111111111111", Permissions.GolfTeeSheetView },
        { "POST", "guests", Permissions.GolfGuestManage },
        { "POST", "guests/11111111-1111-1111-1111-111111111111/status", Permissions.GolfGuestManage },
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
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Authenticated_user_without_the_permission_is_rejected_with_403(
        string method, string path, string permission)
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        // Hold every *other* golf permission and withhold only this route's, so a 403
        // proves the route is gated on that specific permission rather than on being an
        // authenticated golf user.
        var withheld = string.Join(',', GolfApiFactory.GolfPermissions.All
            .Where(p => !string.Equals(p, permission, StringComparison.Ordinal)));

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, withheld);

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Permission_holder_reaches_the_action(string method, string path, string permission)
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, permission);

        using var response = await SendAsync(client, method, path);

        // Read routes answer from the stubs with 200. A command route that supplies no
        // body is rejected by model validation, which still proves the gate opened: the
        // only statuses that would indicate a broken gate are 401 and 403.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Separation of duty: the sensitive golf actions are not interchangeable ----

    [Fact]
    public async Task Course_view_permission_cannot_manage_courses()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfCourseView);

        using var response = await client.PostAsync($"{Prefix}/courses", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tee_sheet_view_permission_cannot_manage_tee_slots()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfTeeSheetView);

        using var response = await client.PostAsync(
            $"{Prefix}/tee-slots/11111111-1111-1111-1111-111111111111/status",
            content: new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Booking_view_permission_cannot_create_a_booking()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfBookingView);

        using var response = await client.PostAsync($"{Prefix}/bookings", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Booking_edit_permission_cannot_cancel_a_booking()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfBookingEdit);

        using var response = await client.PostAsync(
            $"{Prefix}/bookings/11111111-1111-1111-1111-111111111111/cancel",
            content: new StringContent("{\"reason\":\"weather\"}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Booking_cancel_permission_cannot_check_in()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfBookingCancel);

        using var response = await client.PostAsync(
            $"{Prefix}/bookings/11111111-1111-1111-1111-111111111111/check-in",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// GOLF-011: confirming a booking over capacity needs the sensitive override
    /// permission, which is not implied by the ordinary edit permission.
    /// </summary>
    [Fact]
    public async Task Booking_edit_permission_cannot_confirm_with_a_capacity_override()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfBookingEdit);

        using var response = await client.PostAsync(
            $"{Prefix}/bookings/11111111-1111-1111-1111-111111111111/confirm",
            content: new StringContent(
                "{\"overrideCapacity\":true,\"overrideReason\":\"walking event\"}",
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Booking_override_permission_alone_cannot_confirm_without_the_edit_permission()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfBookingOverride);

        using var response = await client.PostAsync(
            $"{Prefix}/bookings/11111111-1111-1111-1111-111111111111/confirm",
            content: new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Golf_charge_permission_cannot_manage_guests()
    {
        using var factory = GolfApiFactory.Create();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, Permissions.GolfChargeCreate);

        using var response = await client.PostAsync($"{Prefix}/guests", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A cashier holds no golf permission at all, so no golf route may be reachable —
    /// including the read-only tee sheet.
    /// </summary>
    [Fact]
    public async Task A_cashier_cannot_reach_any_golf_route()
    {
        using var factory = GolfApiFactory.Create();
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

    /// <summary>
    /// The Phase 8 §11 routes are reproduced exactly, so the documented surface is
    /// pinned by a test rather than left to drift with the controllers.
    /// </summary>
    [Fact]
    public void Every_Phase8_documented_golf_route_is_registered_with_its_documented_permission()
    {
        var documented = new (string Method, string Path, string Permission)[]
        {
            ("GET", "courses", Permissions.GolfCourseView),
            ("GET", "tee-sheet", Permissions.GolfTeeSheetView),
            ("GET", "tee-slots", Permissions.GolfTeeSheetView),
            ("POST", "bookings", Permissions.GolfBookingCreate),
            ("GET", "bookings/11111111-1111-1111-1111-111111111111", Permissions.GolfBookingView),
            ("POST", "bookings/11111111-1111-1111-1111-111111111111/confirm", Permissions.GolfBookingEdit),
            ("POST", "bookings/11111111-1111-1111-1111-111111111111/cancel", Permissions.GolfBookingCancel),
            ("POST", "bookings/11111111-1111-1111-1111-111111111111/check-in", Permissions.GolfBookingCheckIn),
            ("POST", "bookings/11111111-1111-1111-1111-111111111111/no-show", Permissions.GolfBookingEdit),
        };

        foreach (var (method, path, permission) in documented)
        {
            var found = false;
            foreach (var row in Routes)
            {
                if (string.Equals((string)row[0], method, StringComparison.Ordinal)
                    && string.Equals((string)row[1], path, StringComparison.Ordinal)
                    && string.Equals((string)row[2], permission, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, $"Phase 8 §11 route {method} /{Prefix}/{path} is not covered with {permission}.");
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
/// Serves the real D7 golf controllers under the real permission policies, with stub
/// golf services and a header-driven authentication handler.
/// </summary>
internal static class GolfApiFactory
{
    public static TestServer Create()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(GolfCoursesController).Assembly);

        // The application's own policy definitions, so a policy change is not duplicated.
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in GolfPermissions.All)
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

        builder.Services.AddSingleton<IGolfCourseService, StubGolfCourseService>();
        builder.Services.AddSingleton<IGolfTeeSlotService, StubGolfTeeSlotService>();
        builder.Services.AddSingleton<IGolfGuestService, StubGolfGuestService>();
        builder.Services.AddSingleton<IGolfBookingService, StubGolfBookingService>();
        builder.Services.AddSingleton<IGolfChargeService, StubGolfChargeService>();

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.StartAsync().GetAwaiter().GetResult();
        return app.GetTestServer();
    }

    /// <summary>
    /// Every D7 golf permission that the controllers use. The tournament and scoring
    /// permissions are deliberately absent: Phase 6 §15 places tournaments in D8, so
    /// no D7 route is gated on them.
    /// </summary>
    internal static class GolfPermissions
    {
        public static readonly string[] All =
        [
            Permissions.GolfCourseView,
            Permissions.GolfCourseManage,
            Permissions.GolfTeeSheetView,
            Permissions.GolfTeeSlotManage,
            Permissions.GolfBookingView,
            Permissions.GolfBookingCreate,
            Permissions.GolfBookingEdit,
            Permissions.GolfBookingCancel,
            Permissions.GolfBookingOverride,
            Permissions.GolfBookingCheckIn,
            Permissions.GolfGuestManage,
            Permissions.GolfChargeCreate,
        ];
    }
}
