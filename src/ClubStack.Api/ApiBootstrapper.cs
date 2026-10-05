using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ClubStack.Api.Authorization;
using ClubStack.Application;
using ClubStack.Application.Authorization;
using ClubStack.Application.Correlation;
using ClubStack.Contracts.Common;
using ClubStack.Infrastructure;
using ClubStack.Infrastructure.Golf;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Options;
using ClubStack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;
using Swashbuckle.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace ClubStack.Api;

/// <summary>
/// Builds the ClubStack ASP.NET Core application. Both the standalone API entry
/// point and the ClubStack Host Windows service use this bootstrapper so the
/// pipeline is identical regardless of runner.
/// </summary>
public static class ApiBootstrapper
{
    public const string ServiceName = "ClubStack Host";

    public static async Task<WebApplication> CreateApplicationAsync(string[] args, bool runAsWindowsService = false)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext();
        });

        if (runAsWindowsService)
        {
            builder.Host.UseWindowsService(options => options.ServiceName = ServiceName);
        }

        ConfigureServices(builder);

        var app = builder.Build();

        ConfigurePipeline(app);

        await RunBootstrapTasksAsync(app);

        return app;
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddApplication();
        services.AddInfrastructure(builder.Configuration);

        services.AddProblemDetails();

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Type = "https://clubstack.local/problems/validation",
                    Extensions = { ["code"] = ApiErrorCode.Validation },
                };

                problem.Extensions["errors"] = context.ModelState
                    .Where(e => e.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        e => e.Key,
                        e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray());

                foreach (var error in context.ModelState.Values.SelectMany(v => v!.Errors))
                {
                    if (!string.IsNullOrEmpty(error.ErrorMessage))
                    {
                        problem.Detail = string.IsNullOrEmpty(problem.Detail)
                            ? error.ErrorMessage
                            : problem.Detail;
                    }
                }

                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
            };
        });

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "ClubStack.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments(ApiConstants.ApiRoutePrefix))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments(ApiConstants.ApiRoutePrefix))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var permission in Permissions.All)
            {
                options.AddPermissionPolicy(permission);
            }

            // D5: POS member identification may be performed by any cashier or
            // back-office role that holds one of the listed permissions.
            options.AddAnyPermissionPolicy(
                "PermissionAny:PosMemberLookup",
                Permissions.PosProductView,
                Permissions.PosSaleCreate,
                Permissions.MembershipMemberView);
        });

        services.AddHealthChecks()
            .AddCheck<Health.DatabaseHealthCheck>("database", tags: ["ready"]);

        services.AddExceptionHandler<ErrorHandling.GlobalExceptionHandler>();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ClubStack API",
                Version = "v1",
                Description = "ClubStack engineering foundation API (D0).",
            });
        });
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        app.UseSerilogRequestLogging();

        app.UseMiddleware<Correlation.CorrelationIdMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ClubStack API v1"));

        app.UseExceptionHandler();

        app.UseRouting();
        app.UseAuthentication();

        // Enforce the server-side session model (Phase 8 §23) after authentication
        // and before authorization. Session violations terminate the session and
        // produce a 401 with a machine-readable code.
        app.UseMiddleware<Security.SessionEnforcementMiddleware>();

        app.UseAuthorization();

        app.MapControllers();

        app.MapGet("/", () => Results.Ok(new
        {
            application = "ClubStack",
            status = "running",
            api = ApiConstants.ApiRoutePrefix,
        }));

        // Liveness vs readiness: liveness only proves the process is up;
        // readiness also exercises the database health check.
        app.MapHealthChecks(ApiConstants.HealthLivenessPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false,
        });

        app.MapHealthChecks(ApiConstants.HealthReadinessPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        ServeClientAppIfPresent(app);
    }

    /// <summary>
    /// Hosts the built client app (Vite <c>dist</c> copied into <c>wwwroot</c>) when
    /// it exists. SPA deep links fall back to <c>index.html</c>, but API paths are
    /// excluded so unknown endpoints keep their contract 404s. The fallback is
    /// anonymous so the shell can render the client-side sign-in page.
    /// </summary>
    private static void ServeClientAppIfPresent(WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath;
        var indexFile = webRoot is null
            ? Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html")
            : Path.Combine(webRoot, "index.html");

        if (!File.Exists(indexFile))
        {
            return;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapFallback(context =>
        {
            if (context.Request.Path.StartsWithSegments(ApiConstants.ApiRoutePrefix))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            context.Response.ContentType = "text/html";
            return context.Response.SendFileAsync(indexFile);
        }).AllowAnonymous();
    }

    private static async Task RunBootstrapTasksAsync(WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<AppOptions>>().Value;
        if (!options.MigrateOnStartup)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ApiBootstrapper));
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();

        try
        {
            await migrator.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");

            // Reference data (permissions, baseline roles, starter currencies/
            // payment methods) is required for the bootstrap endpoints and dev seed.
            var referenceSeeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
            await referenceSeeder.SeedAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<IDevelopmentDataSeeder>();
            await seeder.SeedAsync();

            var golfSeeder = scope.ServiceProvider.GetRequiredService<GolfConfigurationSeeder>();
            await golfSeeder.SeedAsync();
        }
        catch (Exception exception) when (!options.StopOnMigrationFailure)
        {
            logger.LogError(exception, "Database migration failed; continuing without migrations (StopOnMigrationFailure=false).");
        }
    }
}