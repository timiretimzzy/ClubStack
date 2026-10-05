# Local setup

## Database credential

ClubStack reads the PostgreSQL password from the `CLUBSTACK_DB_PASSWORD` environment
variable. `src/ClubStack.Api/appsettings.json` contains only a token:

```json
"Default": "Host=localhost;Port=5432;Database=clubstack;Username=clubstack;Password=${CLUBSTACK_DB_PASSWORD};Pooling=true;Maximum Pool Size=20"
```

`ConfigurationSecretExpander.Expand` in
`src/ClubStack.Infrastructure/InfrastructureServiceCollectionExtensions.cs` substitutes the
token from the process environment. Nothing sensitive is committed to the repository.

### Configure it once

The password is stored in the **user-scope environment** on this machine, so every new shell
and every `dotnet` invocation resolves it automatically. It is never written to git, source,
appsettings, tests, reports or console output.

To set or replace it (PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Setup-LocalSecrets.ps1
```

The script prompts without echoing, or accepts `-Password`. It reports only presence and
length, never the value. To set it inline instead:

```powershell
[Environment]::SetEnvironmentVariable('CLUBSTACK_DB_PASSWORD', '<password>', 'User')
```

To confirm it resolves, without printing the value:

```powershell
$v = [Environment]::GetEnvironmentVariable('CLUBSTACK_DB_PASSWORD', 'User')
[bool]$v      # True
$v.Length     # length only
```

The value is read from `HKCU\Environment`, which Windows propagates into every newly launched
process for this user. Open a new terminal (or start the app fresh) after changing it; an
already-running shell keeps its old process environment.

### Requirements on the local PostgreSQL role

The integration tests create and drop a throwaway database per fixture, so the role needs
`CREATEDB`:

```sql
SELECT rolcreatedb FROM pg_roles WHERE rolname = 'clubstack';
```

Override the endpoint with `CLUBSTACK_DB_HOST`, `CLUBSTACK_DB_PORT` and `CLUBSTACK_DB_USER`
if your local instance differs from `localhost:5432`.

## Fiscal device credentials (TEST only)

The activation key and fiscal license are separate from the database credential and are
handled as write-only values: they are accepted on the provisioning request, passed to the
device, and never persisted, audited, echoed by status endpoints or written to logs. See
`D5.5_Final_Test_Fiscalisation_Execution_Report.md`.

The live TEST device tests are opt-in and skip unless explicitly configured:

| Variable | Purpose |
| --- | --- |
| `D55_TEST_DEVICE_ID` | Enables the live device tests; when unset they log `SKIPPED` and pass |
| `D55_TEST_SERIAL` | Device serial |
| `D55_TEST_ENVIRONMENT` | `TEST` (default) or `PRODUCTION` |
| `D55_TEST_ACTIVATION_KEY` | Write-only activation key |
| `D55_TEST_LICENSE` | Write-only license key |

Do not set these for a normal `dotnet test` run: the rest of the suite is hermetic and uses a
fake device, and the live tests contact a real ZIMRA host.
