# PoolPredict

PoolPredict is a SaaS platform for private and public prediction pools around real-world tournaments.

The MVP focuses on:

* FIFA World Cup 2026
* Virtual points only
* Automatic market generation
* Automatic settlement
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

```powershell
dotnet run --project apps/api/PoolPredict.Api.csproj

> **Note:** If you encounter a port conflict (e.g., "Address already in use"), terminate the existing process using:
> - **Windows:** `netstat -ano | findstr :5080` to get the PID, then `taskkill /PID <PID> /F`.
> - **Unix/macOS:** `lsof -ti :5080 | xargs kill`.
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

The development admin is seeded from `apps/api/appsettings.Development.json` while the API still uses in-memory identity storage.

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

The current implementation uses in-memory stores, so MariaDB and Redis are not required for normal app testing yet. Running the API and web directly is faster and easier to debug while the first MVP slices are being built.

MariaDB persistence is available for the current MVP data model. To enable it, set the API connection string:

```powershell
$env:ConnectionStrings__MariaDb="Server=localhost;Port=3306;Database=poolpredict;User=poolpredict;Password=poolpredict;"
dotnet run --project apps/api/PoolPredict.Api.csproj
```

When this connection string is not set, the API uses in-memory stores. When it is set, the API applies EF Core migrations at startup. See [docs/DatabaseInitialization.md](docs/DatabaseInitialization.md) for local MariaDB setup.

## Docker Infrastructure

Use Docker only when you want infrastructure services running:

```powershell
docker compose up -d mariadb redis
```

Later, after Redis caching and background jobs are wired in, Docker Compose should become the normal local setup.
