# PoolPredict

PoolPredict is a SaaS platform for private and public prediction pools around real-world tournaments.

The MVP focuses on:

* FIFA World Cup 2026
* Virtual points only
* Automatic market generation
* Manual PlatformAdmin settlement for MVP
* Global Point Payout Configuration managed by Platform Admin

## Repository Layout

```text
apps/
  api/   .NET 10 API
  web/   Next.js app shell
docker/
  caddy/
docs/
```

Current implementation status is tracked in [docs/ImplementationStatus.md](docs/ImplementationStatus.md).

## Local API

The API requires MariaDB. Create the local database, apply migrations, then start the API:

```sql
CREATE DATABASE pool_predict CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
dotnet ef database update --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj
dotnet run --project apps/api/PoolPredict.Api.csproj
```

If you encounter a port conflict, terminate the existing process:

```powershell
netstat -ano | findstr :5080
taskkill /PID <PID> /F
```

Health check:

```text
GET http://localhost:5080/health
```

Development admin user:

```text
Email: admin@poolpredict.local
Password: Admin123!
Role: PlatformAdmin
```

The development admin is seeded from `apps/api/appsettings.Development.json`.

New email/password registrations stay pending until a PlatformAdmin activates the account from `/admin/users`.

## Local App Testing

For current MVP development, run the API and web app directly on your machine instead of using Docker for the whole stack.

API:

```powershell
dotnet run --project apps/api/PoolPredict.Api.csproj
```

Web:

```powershell
cd apps/web
npm run dev -- --hostname localhost --port 3000
```

Open:

```text
http://localhost:3000
```

Redis is not required for normal app testing yet. Running the API and web directly is faster and easier to debug while the first MVP slices are being built.

MariaDB is required for the API. If the database already exists, apply EF Core migrations manually before each API start after schema changes:

```powershell
$env:ConnectionStrings__MariaDb="Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;"
dotnet ef database update --project apps/api/PoolPredict.Api.csproj --startup-project apps/api/PoolPredict.Api.csproj
dotnet run --project apps/api/PoolPredict.Api.csproj
```

The API expects `ConnectionStrings:MariaDb` to be configured and the database schema to already be migrated; it does not run migrations at startup. After the Sprint 7 event-management changes, apply the pending migration before testing admin settlement or provider/manual event mode changes. See [docs/DatabaseInitialization.md](docs/DatabaseInitialization.md) for detailed local MariaDB setup.

The tournament provider defaults to `Mock`. To use football-data.org, set `EventProvider:Provider` to `FootballData` and configure `EventProvider:FootballData:ApiToken`. Platform admins can inspect and trigger provider sync from `/admin/provider`.

Admin database-backup email delivery requires SMTP to be configured and enabled, plus `mariadb-dump` or `mysqldump` to be installed on the API host. If the dump executable is not on `PATH`, configure `DatabaseBackup:DumpExecutablePath`. To keep a local copy of each generated `.zip`, configure `DatabaseBackup:ArchiveDirectory`; the temporary `.sql` file is still deleted after the email flow completes.

## Docker Infrastructure

Use Docker only when you want infrastructure services running:

```powershell
docker compose up -d mariadb redis
```

Later, after Redis caching and background jobs are wired in, Docker Compose should become the normal local setup.
