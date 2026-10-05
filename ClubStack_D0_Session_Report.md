# ClubStack D0 Session Report

Engineering-foundation (D0) fix-up session.

- **Date:** 2026-09-23
- **Scope:** Build repair of `ClubStack.slnx` (net10.0) across `ClubStack.Infrastructure` and `ClubStack.Api`.
- **Outcome:** `dotnet build ClubStack.slnx -c Debug` succeeds with **0 warnings / 0 errors** across all 12 projects; unit test projects pass.

## Background

The solution failed to compile with roughly 30 errors, concentrated in `ClubStack.Infrastructure` and `ClubStack.Api`. Most failures were namespace/API-availability issues caused by package/API version drift (Quartz 4, OpenAPI.NET v2, Npgsql/EF Core 10) and by non-Web classlib projects not receiving `Microsoft.Extensions.DependencyInjection` via implicit usings.

## Changes

### ClubStack.Infrastructure

**`src\ClubStack.Infrastructure\Outbox\OutboxProcessorJob.cs`**
- Corrected the Quartz `IJob.Execute` signature to:
  `public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)`.
  Initial attempts used `Task`, then a single-parameter `ValueTask`; both were rejected by the Quartz contract.
- Added `using Microsoft.Extensions.DependencyInjection;`.
- Changed scope acquisition from `await using _scopeFactory.CreateAsyncScope()` to
  `using var scope = _scopeFactory.CreateScope();` (`CreateAsyncScope` is an `IServiceProvider` extension, not an `IServiceScopeFactory` member).

**`src\ClubStack.Infrastructure\InfrastructureServiceCollectionExtensions.cs`**
- Added `using Microsoft.Extensions.DependencyInjection;`.
- Removed `.AddDefaultTokenProviders()` from the Identity builder chain (not present on `IdentityBuilder` in the referenced package set; token providers are out of scope for D0).
- Fixed nullable-argument warning (CS8604) in `AddIdentityCore` via `configureOptions ?? (_ => { })`.
- Modernized Quartz 4 configuration:
  - Removed `quartz.SchedulerName = "ClubStack";` (read-only in Quartz 4).
  - Removed `quartz.UseMicrosoftDependencyInjectionJobFactory();` (removed API; DI-resolved jobs are the Quartz 4 default).
  - Replaced `schedule.WithIntervalInSeconds(5)` with `schedule.WithInterval(TimeSpan.FromSeconds(5))`.

**`src\ClubStack.Infrastructure\Persistence\ClubStackDbContext.cs`**
- Replaced the three `entity.UseXminAsConcurrencyToken()` calls (ReferenceSequences, IdempotencyRecords, OutboxMessages) with a shared helper because the extension is not exposed by the referenced Npgsql provider:
  ```csharp
  private static void ConfigureConcurrencyToken(EntityTypeBuilder entity)
  {
      entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsConcurrencyToken();
  }
  ```
- Added `using Microsoft.EntityFrameworkCore.Metadata.Builders;` for the non-generic `EntityTypeBuilder`.

**`src\ClubStack.Infrastructure\Auditing\EfAuditRecorder.cs`**
- Fixed CS0266 in `GetRecentAsync`: declared the query as `IQueryable<AuditLogEntity>` instead of `var` so later `Where` chaining still assigns to the `IQueryable` variable typed from the initial `OrderByDescending`.

**`src\ClubStack.Infrastructure\Options\AppOptions.cs`**
- Added the missing `bool StopOnMigrationFailure { get; set; } = true;` property (referenced by API migration bootstrap).

**`src\ClubStack.Infrastructure\ClubStack.Infrastructure.csproj`**
- Verified/completed direct package references:
  - `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12
  - `Microsoft.Extensions.Logging.Abstractions` 10.0.12

### ClubStack.Api

**`src\ClubStack.Api\ApiBootstrapper.cs`**
- Changed `using Microsoft.OpenApi.Models;` to `using Microsoft.OpenApi;` (OpenAPI.NET v2.x moved model types to the root namespace; resolved `Microsoft.OpenApi 2.12.0`).
- Added `using Microsoft.AspNetCore.Mvc;` (was missing `ApiBehaviorOptions`).
- Inserted `.AddMvc()` into the `AddApiVersioning(...)` chain to satisfy diagnostic **AV0013** (version MVC controller-based APIs).
- Replaced `ILogger<ApiBootstrapper>` with `ILoggerFactory().CreateLogger(nameof(ApiBootstrapper))` (static types cannot be used as generic type arguments, CS0718).
- Made `UseWindowsService` compile by adding the missing assembly reference.

**`src\ClubStack.Api\ClubStack.Api.csproj`**
- Added `<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="10.0.12" />`.

**`src\ClubStack.Api\Controllers\AuthController.cs`**
- Resolved the `ISystemClock` ambiguity between `Microsoft.AspNetCore.Authentication.ISystemClock` and `ClubStack.Domain.Common.ISystemClock` by adding an alias:
  `using ISystemClock = ClubStack.Domain.Common.ISystemClock;`

**`src\ClubStack.Api\Controllers\HealthController.cs`**
- Added `using Microsoft.AspNetCore.Authorization;` so `[AllowAnonymous]` resolves.

**`src\ClubStack.Api\ErrorHandling\GlobalExceptionHandler.cs`**
- Resolved the `UnauthorizedAccessException` ambiguity between `System` and `ClubStack.Application.Exceptions` by adding an alias:
  `using UnauthorizedAccessException = ClubStack.Application.Exceptions.UnauthorizedAccessException;`

## Root-cause notes

- "`IServiceCollection`/`IServiceScopeFactory` could not be found" in the Infrastructure classlib was **not** a missing-package problem (the packages were already in `project.assets.json`); it was the absence of `using Microsoft.Extensions.DependencyInjection;` in a `Microsoft.NET.Sdk` (non-Web) project, which does not import that namespace implicitly.
- `Microsoft.OpenApi 2.12.0` in the Api dependency graph confirmed the v1 `Microsoft.OpenApi.Models` namespace no longer exists, guiding the `using Microsoft.OpenApi;` fix.

## Verification

- `dotnet restore ClubStack.slnx` (forced for Infrastructure by clearing `src\ClubStack.Infrastructure\obj`).
- `dotnet build ClubStack.slnx -c Debug` — succeeded:

  ```
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  ```

  Projects building: ClubStack.Contracts, ClubStack.Domain, ClubStack.Hardware, ClubStack.Desktop, ClubStack.Application, ClubStack.Infrastructure, ClubStack.Api, ClubStack.Host, plus test projects Domain.Tests, Application.Tests, Api.Tests, IntegrationTests.

- `dotnet test` (per project, `--no-build`):
  - `ClubStack.Domain.Tests` — 1 passed.
  - `ClubStack.Application.Tests` — 1 passed.
  - `ClubStack.Api.Tests` — 1 passed.
  - `ClubStack.IntegrationTests` — builds; **no test files present yet**.

## Left for a later session

- EF Core migrations for the D0 schema (Identity tables, audit/outbox/idempotency/reference-sequence tables, xmin shadow concurrency tokens) applied against a real PostgreSQL instance.
- Actual integration tests exercising the data foundation (repository/stores, outbox dispatch, idempotency, reference generation) against PostgreSQL.