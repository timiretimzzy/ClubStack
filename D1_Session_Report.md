# ClubStack D1 Session Report

**D1 Platform + Identity + Security (backend-only)**

## 1. Session Objective

Complete the D1 deliverable — Platform, Identity and Security backend — per the approved
specification chain (Phase 8 + Phase 9): the permission catalogue, identity lifecycle,
server-side session model, password management, reference-data seeding, bootstrap of the
initial administrator and club, audited paged queries/export, and server-side authorization
on every new endpoint — while keeping the D0 regression suite green.

## 2. Scope & Boundary

- **In scope (D1):** Platform, Identity, Security (password reset), Phase 9 §27 bootstrap,
  server-side authorization on all new endpoints, audit paged query/export, full permission
  catalogue, 19 baseline roles, reference-data (system/starter) seeding.
- **Explicitly out of scope:** no D2+ modules, no `approval_records`, no reservations, no
  frontend. Business permissions beyond D1 exist in the catalogue as seed data but are not
  enforced on API endpoints until their slice.
- PM-confirmed "D1 backend-only"; implementation role only; note: final system behaviour
  relies on the effectively-anonymous default Kerberos session of the PowerShell persona,
  stabilised during the D0 phase.

## 3. Specification Sources

- `ClubStack_Phase_8_*.md` (Identity, Session §23, Permissions §4, Sensitive actions §5,
  Audit §8.2, Bootstrap gate §27/§21).
- `ClubStack_Phase_9_*.md` (system/starter data §42, bootstrap, admin seeding).
- `ClubStack_D0_Implementation_Package.md` (existing foundation + contract shapes).
- PM decision "D1 backend-only"; D0 build baseline 0 warning / 0 errors.

## 4. Design Decisions

- **Session model (Ph8 §23):** one `user_sessions` row per login, primary claim `session_id`;
  `SessionEnforcementMiddleware` registered between `UseAuthentication` and
  `UseAuthorization`; on violation the session is revoked and a 401 with machine-readable
  code is returned. Sign-out revokes the session server-side.
- **Authorization:** a dedicated policy per permission in `Permissions.All`; controllers use
  `[Authorize(Policy = "Permission:" + Permissions.X)]` via constants. Fallback policy
  requires an authenticated user, so unauthorised endpoints fail closed.
- **Bootstrap gate:** controlled by `App:EnableBootstrap` (dev true) or
  `App:EnableDevelopmentSeed`; repeat returns 409, disabled returns 403/`AccessDenied`.
- **Reference seeding keeps D0 regression honest:** the collection fixture database is
  migrated but *not* reference-seeded; reference data exists only through `ApiBootstrapper`
  startup or explicit seeding. Tests that need roles/permissions/currency seed first.
- **Starter role→permission maps are authored documentation-starter configs** (the spec does
  not define exact baseline matrices); they are validated per module and are non-destructive
  of administrator edits.
- **Security options defaults** live in `AppOptions.SecurityOptions` / `Security` section:
  `RequiredLength = 12`, digits/lower/upper/non-alphanumeric required.

## 5. Permissions Catalogue

- `src/ClubStack.Application/Authorization/Permissions.cs` — **163 permission descriptors**
  covering Platform, Identity, Membership, Credentials, Accounts, Finance, Cashiering, POS,
  Inventory, Procurement, Facilities, Assets, Maintenance, Events, Golf, Guests, Reporting,
  Audit. Keys follow `Module.Resource.Action`, each with `IsSensitive` metadata.
- Seeded as `PermissionEntity` rows with `IsSystem = true`; idempotent and additive.
- Policies generated for every key in `Permissions.All`.

## 6. Baseline Roles

- **19 roles** seeded as system roles (System Administrator, Club Manager, Membership
  Officer, Finance Officer, Cashier, POS Operator, Golf Operations, Tournament Manager,
  Inventory Officer, Procurement Officer, F&B Manager, F&B Operator, Pro Shop Operator,
  Asset Manager, Maintenance Officer, Reception Officer, Event Manager, Auditor, Report
  Viewer) with documented starter permission maps (seeder-owned, not spec-owned).

## 7. Identity Implementation

- `ClubStackUser` / `ClubStackRole` identity stores backed by EF Core (`user_sessions`,
  `roles`, `role_permissions`, `user_roles` schema).
- ASP.NET Core Identity with cookie scheme `ClubStack.Auth` (HttpOnly, SameSite=Lax, Secure,
  sliding 12 h), 401/403 JSON responses for `/api/v1/*` via `OnRedirectToLogin` /
  `OnRedirectToAccessDenied`.
- `IUserStore` + `IRoleStore` custom stores; token providers registered for password reset
  (`.NET 10: AddDefaultTokenProviders removed` → explicit
  `AddTokenProvider<DataProtectorTokenProvider<ClubStackUser>>(TokenOptions.DefaultProvider)`
  + `AddDataProtection()`).

## 8. Session Model

- `user_sessions` row created per successful login (`POST /api/v1/auth/login`).
- `SessionEnforcementMiddleware` validates the `session_id` claim for every authenticated
  request; a missing/revoked/expired session returns 401 and revokes the cookie.
- `POST /api/v1/auth/logout` revokes the row server-side; subsequent requests 401 (verified
  end-to-end).
- Sessions listable/revocable under `/api/v1/sessions` (server-side revocation).

## 9. Authorization & Security

- Policies for every permission; new controllers only accept calls via
  `[Authorize(Policy = ...)]` — server-side authorization, not UI gating.
- Sensitive actions flow through an `IsSensitive` audit record.
- Password rules enforced at user creation and reset (length 12 + complexity); password
  reset uses a one-time token (`POST` reset / initiate flows wired to
  `UpdateSecurityStampAsync`/token provider).

## 10. Password Reset Implementation

- `IUserTwoFactorTokenProvider` + `DataProtectorTokenProvider` token generate/consume;
  `UserManager.ResetPasswordAsync` with security-stamp refresh; all routes server-side
  authorised; reset tokens single-use.

## 11. Server-Side Authorization on Endpoints

Every D1 endpoint (`users`, `roles`, `permissions`, `sessions`, `clubs`, `currencies`,
`payment-methods`, `audit`, `system-configuration`, `auth/me`, bootstrap writes) is
attribute-gated by the matching permission policy and enforced on the server; anonymous
requests receive 401 (verified).

## 12. Reference Data Seeding

- `IReferenceDataSeeder` (`Infrastructure/Identity/ReferenceDataSeeder.cs`): idempotent,
  non-destructive; seeds permission catalogue (163), starter currencies (ZAR default),
  starter payment methods (Cash/Card/Member Account), and 19 baseline roles with starter
  permission joins. Runs from `ApiBootstrapper` after migrate and on demand in tests.

## 13. Bootstrap

- `POST /api/v1/bootstrap/admin` (System Administrator + all permissions),
  `POST /api/v1/bootstrap/club` (club with default currency); gate through
  `App:EnableBootstrap`/`App:EnableDevelopmentSeed`; repeat → 409; disabled → 403;
  `GET /api/v1/bootstrap/status` is anonymous and reflects stage progress.

## 14. Database Migration

- **Migration `20260923185124_D1_PlatformIdentity`** adding the identity/session/
  permissions/audit schema. Applied to the local dev database:
  `Applying migration '20260923185124_D1_PlatformIdentity'. Done.`
- Applied automatically at startup by `ApiBootstrapper` (`MigrateOnStartup`), before
  reference seeding.

## 15. API Surface

- Prefix `/api/v1` (`ApiConstants`). D1 surface: `auth/login`, `auth/logout`, `auth/me`,
  `users/*`, `roles/*`, `permissions`, `sessions`, `clubs`, `currencies`,
  `payment-methods`, `tax-rates`, `system-configuration`, `audit` (paged query + export),
  `bootstrap/*`, health (`/health`, `/health/ready`).

## 16. Build Results

```
Build succeeded. 0 Warning(s) 0 Error(s)   (dotnet build ClubStack.slnx -c Debug)
```

`ClubStack.Infrastructure.csproj` gained `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
and had redundant package references pruned (fixes NU1510-as-error).

### DI correction surfaced by the API-level test

`AddDbContextFactory` registered `IDbContextFactory` as singleton while EF's
`DbContextOptions` was scoped — valid under the default (non-validating) production host,
but rejected by `ValidateOnBuild` (Development). Fixed in
`InfrastructureServiceCollectionExtensions.cs` by registering a **singleton
`DbContextOptions<ClubStackDbContext>`** first; EF's `TryAdd*` then keeps it for both the
scoped `AddDbContext` and the singleton factory. No behavioural change in production.

## 17. Test Results

```
$env:CLUBSTACK_DB_PASSWORD='<set once at User scope: CLUBSTACK_DB_PASSWORD; see docs/local-setup>'; dotnet test ClubStack.slnx
  Domain.Tests ......... Passed  1  0 failed
  Application.Tests .... Passed  1  0 failed
  Api.Tests ............ Passed  1  0 failed
  IntegrationTests ..... Passed 75  0 failed   (53 D0 + 22 D1)
```

D1 test files: `D1ReferenceDataTests.cs`, `D1PlatformAndIdentityTests.cs`,
`D1BootstrapTests.cs`, `D1AuditQueryTests.cs`, `D1AuditExportTests.cs`. Harness extended
with `BuildAgainstFreshDatabaseAsync(additionalConfiguration)` overloads.
`D1AuditExportTests` drives the **real `Program` pipeline** via
`WebApplicationFactory<Program>` against a hermetic scratch database (deterministic
`DbContextOptions` DI swap; run repeatedly with no flakiness).

## 18. End-to-End Smoke Test (live HTTPS, real cookie)

```
bootstrap.status  -> completed:false
bootstrap.admin   -> administratorCreated:true
bootstrap.club    -> completed:true (clubId 01a0d1a0-...-d45d25d)
login             -> 200 (session cookie issued)
/auth/me, /users, /roles, /permissions, /clubs, /currencies,
/payment-methods, /tax-rates, /system-configuration, /audit, /sessions -> 200
anonymous /users  -> 401
logout            -> 204 (server-side session revoked)
post-logout /users -> 401 (session enforcement confirmed)
```

### Audit CSV export verified (API-level, real pipeline)

`GET /api/v1/audit/export` verified end-to-end against the real HTTP pipeline with a
cookie session and a System Administrator granted `Audit.Export`/`Audit.View`:

- **Unauthorized:** anonymous request → 401.
- **Content type/headers:** `text/csv`, `Content-Disposition:
  attachment; filename="clubstack-audit-<timestamp>.csv"`.
- **BOM:** UTF-8 preamble (EF BB BF) present before the header row.
- **Header:** `id,timestamp_utc,actor_user_id,actor_name,action,entity_type,entity_id,reference,result,correlation_id`.
- **Records & escaping:** cells containing comma, embedded quote and newline are RFC-4180
  quoted with doubled quotes (e.g. `"Doe, ""J"" \n Jr"`), embedded-quote ids emit `"e""2"`,
  comma-only cells emit `"Risk,Review"` (never raw), empty result emits an empty unquoted
  cell (`REF-2,,corr-2`), and a multi-line cell still counts as one logical CSV record
  (confirmed via `pageSize=1`).
- **Filtering:** `action=` returns only matching rows; `actorUserId=` returns only that
  actor's rows.
- **Paging:** `pageSize=n` is honored (newest first).
- **Empty result:** header row only, single trailing newline, no data records.

## 19. D0-Test Maintenance Decisions

- `MigrationStartupBehaviorTests` updated: migration count `1 → 2`, roles `== 0 → > 0`
  (system seed now present), users `== 0` (dev seed skipped at startup) — legitimate
  spec-mandated behaviour changes, documented in-repo; not test weakening.
- Collection fixture explicitly *not* reference-seeded by default to preserve the D0 test
  contract (reference data only appears via `ApiBootstrapper` or explicit seeding).

## 20. Verification Status & Next Steps

- **Status: GREEN.** Solution builds 0/0; 78 total tests pass (75 integration incl. 53 D0);
  migration applied; live HTTPS smoke covers bootstrap → login → authorised reads →
  anonymous/revoked 401s; audit CSV export verified via a dedicated API-level test
  (authorized/unauthorized, headers, content type, BOM, records, escaping, filtering,
  paging, empty result).
- Historical flake during verification was traced to the test host ignoring the
  `ConfigureAppConfiguration` override for minimal-hosting apps (the factory briefly used
  the shared dev database); resolved by swapping `DbContextOptions` in DI, and the stray
  `audit-exporter` test user was removed from the local dev database.
- **Next (post-review):** proceed to enabling individual business-slice endpoints
  (Membership/Accounts first) reusing the D1 permission/session/audit plumbing.