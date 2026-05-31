# Implementation Status

Last updated: 2026-05-31

## Current State

The project has an initial MVP foundation using a DDD-lite structure.

The web app is currently only a shell. Working functionality is available through API endpoints.

## Completed

### Repository Foundation

* Added repository layout:
  * `apps/api`
  * `apps/web`
  * `docker`
  * `docs`
* Added `.gitignore`
* Added `global.json`
* Added `PoolPredict.sln`
* Added root `README.md`

### Documentation

* Clarified MVP market scope, including Handicap
* Added point rules:
  * points deducted on prediction submit
  * negative balances allowed
  * members with negative balances cannot submit new predictions
* Added multi-language requirement:
  * English first
  * admin-managed translations later
  * fallback to English
* Added Point Payout Configuration instead of public betting odds
* Clarified Platform Admin global payout defaults
* Clarified prediction-time snapshots for line values and payout configuration
* Clarified UUID v7 database ID strategy
* Updated README with local testing guidance

### API

Added .NET 10 minimal API foundation.

Implemented endpoints:

* `GET /health`
* `GET /api/tournaments`
* `GET /api/tournaments/{tournamentId}/events`
* `POST /api/pools`
* `GET /api/pools/{poolId}/markets`
* `POST /api/predictions`
* `GET /api/predictions/pool/{poolId}`
* `GET /api/predictions/balance?poolId={poolId}&memberId={memberId}`

Implemented in-memory domain slice:

* Tournaments
* Events
* Pools
* Market profiles
* Markets
* Predictions
* Point ledger entries

Current seeded data:

* FIFA World Cup 2026
* Three placeholder events

Current Standard pool behavior:

* Creates full-time and first-half markets
* Generates Winner, Handicap, Over/Under, Odd/Even and Correct Score markets
* With current seed data, Standard profile creates 30 markets

Prediction behavior:

* Deducts stake immediately
* Adds starting balance ledger entry for first member action
* Blocks new predictions when member balance is negative
* Snapshots:
  * market type
  * market period
  * line value
  * payout multiplier
  * payout configuration version

### Web

Added a Next.js app shell at `apps/web`.

Current web behavior:

* Shows intro/status text
* Links to API health
* Does not yet provide working pool/prediction UI

### Infrastructure

Added Docker Compose with:

* MariaDB
* Redis
* API
* Web
* Caddy

Current recommendation:

* Run API and web directly for local testing
* Use Docker only for MariaDB/Redis until persistence and jobs are wired in

## Verified

Commands verified:

```powershell
dotnet build PoolPredict.sln
```

```powershell
cd apps/web
npm run build
```

Manual smoke tests verified:

* API health returns OK
* Tournament list returns seeded World Cup 2026
* Pool creation works
* Standard profile generates 30 markets
* Prediction submission deducts points
* Balance endpoint returns updated balance

## Known Gaps

* No database persistence yet
* No authentication yet
* No real membership model yet
* No invite links yet
* No settlement engine yet
* No admin UI for payout configuration or line overrides yet
* No localization implementation yet
* Web UI has no working forms yet
* No tests yet
* Repository is not initialized as a Git repo yet

## Next Recommended Step

Build a basic MVP web UI over the existing API:

1. List tournaments
2. Create a pool
3. Show generated markets
4. Submit a prediction
5. Show member balance

After that, add persistence with MariaDB and replace the in-memory stores.
