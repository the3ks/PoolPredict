# Database Initialization

Last updated: 2026-06-01

## Local MariaDB Connection

For the local MariaDB instance:

```text
Host: 127.0.0.1
Port: 3308
Database: pool_predict
User: root02
Password: 123
```

The API requires this connection string:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
```

Apply migrations, then start the API:

```powershell
dotnet ef database update --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj
dotnet run --project apps/api/PoolPredict.Api.csproj
```

The API listens on:

```text
http://localhost:5080
```

## Initialization Behavior

The API uses EF Core migrations, but it does not apply them automatically at startup.

Before starting the API, apply pending migrations manually:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
dotnet ef database update --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj
```

On startup, when the database schema is already migrated, the API:

1. Connects to MariaDB.
2. Seeds the development PlatformAdmin user.
3. Syncs the mock World Cup 2026 tournament catalog into the database if tournament data does not exist.
4. Seeds the MVP global payout defaults if no payout configuration exists.
5. Uses database-backed stores for identity, pools, invites, markets, predictions, point ledger, payout defaults and settlement run logs.

## First-Time Setup

Create the database before running the API:

```sql
CREATE DATABASE pool_predict CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Then run:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
dotnet ef database update --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj
dotnet run --project apps/api/PoolPredict.Api.csproj
```

After startup, check:

```text
GET http://localhost:5080/health
```

## Applying Future Schema Changes

When the EF model changes, create a migration:

```powershell
dotnet ef migrations add MigrationName --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj --output-dir Infrastructure/Persistence/Migrations
```

Then apply pending migrations manually:

```powershell
dotnet ef database update --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj
```

Use the same connection string environment variable before running migration commands:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
```

## Resetting A Development Database

If the database contains tables created by the old `EnsureCreated` spike, drop and recreate it once so migrations can own the schema history.

Development reset:

```sql
DROP DATABASE pool_predict;
CREATE DATABASE pool_predict CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Then run `dotnet ef database update` and restart the API.

## Current Tables

The current schema covers:

* `users`
* `user_external_logins`
* `tournaments`
* `participants`
* `events`
* `event_results`
* `pools`
* `pool_members`
* `pool_invites`
* `markets`
* `predictions`
* `point_ledger`
* `payout_configurations`
* `payout_configuration_market_rules`
* `settlement_runs`
* `settlement_logs`

Provider-originated tournament tables include provider/source metadata so Mock test data and real provider data can coexist without external ID collisions. Events also store `management_mode` so PlatformAdmin can switch individual events between provider-managed and manually managed behavior.

## Development Admin

Seeded when the API starts:

```text
Email: admin@poolpredict.local
Password: Admin123!
Role: PlatformAdmin
```

## Optional FootballData Provider

Mock provider is the default. To use football-data.org for tournament import, configure:

```json
"EventProvider": {
  "Provider": "FootballData",
  "FootballData": {
    "BaseUrl": "https://api.football-data.org/v4",
    "ApiToken": "YOUR_TOKEN",
    "CompetitionCode": "WC",
    "Season": 2026,
    "TournamentName": "FIFA World Cup 2026",
    "Sport": "Football",
    "StartsOn": "2026-06-11",
    "EndsOn": "2026-07-19"
  }
}
```

PlatformAdmin users can trigger provider sync from `/admin/provider`.

Startup behavior:

* `Mock` provider auto-seeds when the database has no tournament data.
* `FootballData` provider does not auto-sync on empty database startup. Use `/admin/provider` and click Sync provider so real external imports are explicit.

Do not commit real API tokens. Keep them in `apps/api/appsettings.Development.json`, user secrets or environment variables.

## Later Migration Work

Before production, review generated migration scripts before applying them to shared environments.
