# Database Initialization

Last updated: 2026-05-31

## Local MariaDB Connection

For the local MariaDB instance:

```text
Host: 127.0.0.1
Port: 3308
Database: pool_predict
User: root02
Password: 123
```

Use this API connection string:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
```

Then start the API:

```powershell
dotnet run --project apps/api/PoolPredict.Api.csproj
```

The API listens on:

```text
http://localhost:5080
```

## Initialization Behavior

The API uses EF Core migrations.

On startup, when `ConnectionStrings:MariaDb` is set, the API:

1. Connects to MariaDB.
2. Applies pending migrations with `Database.Migrate()`.
3. Seeds the development PlatformAdmin user.
4. Syncs the mock World Cup 2026 tournament catalog into the database if tournament data does not exist.
5. Uses database-backed stores for identity, pools, invites, markets, predictions and point ledger.

## First-Time Setup

Create the database before running the API:

```sql
CREATE DATABASE pool_predict CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Then run:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
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

Then either start the API, which applies pending migrations automatically, or run:

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

Then restart the API with the connection string.

## Current Tables

The current schema covers:

* `users`
* `user_external_logins`
* `tournaments`
* `participants`
* `events`
* `pools`
* `pool_members`
* `pool_invites`
* `markets`
* `predictions`
* `point_ledger`

## Development Admin

Seeded when the API starts:

```text
Email: admin@poolpredict.local
Password: Admin123!
Role: PlatformAdmin
```

## Later Migration Work

Before production, review migration scripts and replace automatic startup migration with a controlled deployment migration step.
