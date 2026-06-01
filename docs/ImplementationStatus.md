# Implementation Status

Last updated: 2026-06-01

## Current State

The project has an initial MVP foundation using a DDD-lite structure.

The web app now includes Sprint 1 authentication and Sprint 2 pool management screens backed by authenticated API endpoints. Authentication and pool management are separated into dedicated routes under the planned app structure.

Sprint 3 tournament infrastructure is implemented with participant records, an event provider abstraction and a mock World Cup provider. Sprint 4 external integration is implemented with configurable provider selection, a FootballData provider adapter and an admin-triggered tournament sync path. Sprint 5 market and settlement foundation is implemented with DB-backed payout defaults, profile-based market generation and settlement run/log tables. Sprint 6 prediction submission is started with authenticated prediction entry, member balances and prediction history. MariaDB-backed EF Core persistence is now required for the API.

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
* Added `docs/UIStandards.md` for dark-theme UI rules and future AI-assisted frontend work

### API

Added .NET 10 minimal API foundation.

Implemented endpoints:

* `GET /health`
* `POST /api/auth/register`
* `POST /api/auth/login`
* `POST /api/auth/google`
* `GET /api/auth/me`
* `GET /api/tournaments`
* `GET /api/tournaments/{tournamentId}/events`
* `GET /api/tournaments/{tournamentId}/participants`
* `GET /api/tournaments/provider/status`
* `POST /api/tournaments/sync`
* `GET /api/markets/payout-configurations`
* `POST /api/pools`
* `GET /api/pools`
* `GET /api/pools/{poolId}`
* `PUT /api/pools/{poolId}`
* `POST /api/pools/{poolId}/invites`
* `GET /api/pools/invites/{code}`
* `POST /api/pools/join`
* `GET /api/pools/{poolId}/markets`
* `POST /api/predictions`
* `GET /api/predictions/pool/{poolId}`
* `GET /api/predictions/balance?poolId={poolId}`

Implemented domain slice backed by MariaDB persistence:

* Identity users
* User external logins
* Tournaments
* Participants
* Events
* Pools
* Pool members
* Pool invites
* Market profiles
* Markets
* Predictions
* Point ledger entries

Current seeded data:

* FIFA World Cup 2026
* Six mock participants
* Three placeholder events

Tournament behavior:

* Event provider selection is configurable through `EventProvider:Provider`
* `TournamentCatalog` loads from persistence first
* If no persisted tournament data exists, the catalog syncs from `IEventProvider`
* `MockEventProvider` provides World Cup 2026 tournament, participants and events
* `FootballDataProvider` can import teams and matches from football-data.org when configured
* `TournamentSyncJob` supports admin-triggered provider sync
* `FootballData` sync is explicit from Admin and does not auto-import on empty database startup
* Provider data is saved to MariaDB

Current Standard pool behavior:

* Creates full-time and first-half markets
* Generates Winner, Handicap, Over/Under, Odd/Even and Correct Score markets
* Generates market lines and payout multipliers from the active payout configuration
* With current seed data, Standard profile creates 30 markets

Market configuration behavior:

* Seeds DB-backed MVP global payout defaults when no payout configuration exists
* Supports Casual, Standard and Expert profile rule sets
* Stores payout configuration versions and market rules in MariaDB when configured
* Exposes payout defaults to PlatformAdmin users from `GET /api/markets/payout-configurations`

Settlement foundation behavior:

* Adds persisted settlement run records for future idempotent event settlement
* Adds persisted settlement log records linked to settlement runs
* Settlement calculation is not implemented yet

Prediction behavior:

* Prediction endpoints require authentication
* Prediction submission derives the pool member from the signed-in user
* Deducts stake immediately
* Adds starting balance ledger entry for first member action
* Blocks new predictions when member balance is negative
* Blocks prediction submission after the market event start time
* Snapshots:
  * market type
  * market period
  * line value
  * payout multiplier
  * payout configuration version

Authentication behavior:

* Registers email/password users
* Hashes passwords with PBKDF2-SHA256
* Issues signed JWT bearer tokens
* Validates bearer tokens for protected profile access
* Supports an MVP Google-login endpoint that links a Google provider subject to a user account
* Seeds a development-only PlatformAdmin from `apps/api/appsettings.Development.json`
* Stores identity data in MariaDB

Pool behavior:

* Pool creation requires an authenticated user
* Pool creator is recorded as the owner member
* Authenticated users can list only pools they belong to
* Pool owners/admins can edit pool name and starting balance
* Pool owners/admins can create invite codes
* Authenticated users can join pools by invite code
* Pool summaries include member and invite counts

### Web

Added a Next.js app shell at `apps/web`.

Current web behavior:

* Shows running/upcoming tournaments publicly on `/`
* Provides login and registration forms on `/login` and `/register`
* Provides an MVP Google-login form
* Persists the JWT access token in browser local storage
* Loads the signed-in profile from `GET /api/auth/me`
* Provides a signed-in app shell under `/app`
* Shows running/upcoming tournaments on the signed-in dashboard
* Loads tournaments for pool creation
* Creates pools for the signed-in user on `/app/pools/new`
* Lists the signed-in user's pools on `/app/pools`
* Shows pool overview and editable owner/admin settings on `/app/pools/[poolId]`
* Creates invite codes for owner/admin pools on `/app/pools/[poolId]/invites`
* Joins pools by invite code on `/app/pools/join`
* Shows signed-in profile details on `/app/profile`
* Shows provider status and admin-triggered sync on `/app/admin` for PlatformAdmin users
* Shows active payout defaults on `/app/admin` for PlatformAdmin users
* Shows pool markets grouped by match on `/app/pools/[poolId]`
* Allows signed-in pool members to submit predictions from available markets
* Shows current member balance and prediction history on the pool page
* Links to API health
* Uses a dark public-end-user theme across public, auth and signed-in app routes
* Uses local UI primitives for page headers, panels, status pills, stat tiles and icon labels
* Uses `lucide-react` for lightweight, consistent interface icons
* Provides first working prediction UI for generated pool markets

### Infrastructure

Added Docker Compose with:

* MariaDB
* Redis
* API
* Web
* Caddy

Added required API persistence foundation:

* `Pomelo.EntityFrameworkCore.MySql`
* `PoolPredictDbContext`
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
* `payout_configurations`
* `payout_configuration_market_rules`
* `settlement_runs`
* `settlement_logs`
* EF Core initial migration
* Manual EF Core migration application before API startup

Current recommendation:

* Run API and web directly for local testing
* Use local MariaDB or Docker MariaDB for all API testing
* Redis is not required yet because caching and jobs are not wired in

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
* Email registration returns a JWT and profile
* Authenticated profile lookup accepts the JWT
* MVP Google login returns a JWT and profile
* Tournament list returns seeded World Cup 2026
* Participant list returns mock World Cup participants
* Pool creation works
* Authenticated pool list returns only the user's pools
* Pool update works for owner/admin members
* Invite creation works for owner/admin members
* Join by invite code works for authenticated users
* Standard profile generates 30 markets
* API build passes after Sprint 5 migration
* Web production build passes after Sprint 5 admin UI update
* Prediction submission deducts points
* Balance endpoint returns updated balance
* API build passes after Sprint 6 authenticated prediction changes
* Web production build passes after Sprint 6 prediction UI update

## Known Gaps

* Shared environments should review generated migration scripts before applying them
* API startup fails when `ConnectionStrings:MariaDb` is missing
* Google login does not yet validate a real Google ID token or OAuth client configuration
* Pool member management is limited to owner/member roles and invite-code joins
* Invite links are represented as invite codes; full URL routing is not implemented yet
* FootballData sync requires an API token and has not been smoke-tested against a real provider account
* Tournament sync is manually triggered from admin UI; recurring background scheduling is not implemented yet
* Settlement tables exist, but no settlement engine yet
* Admin UI displays payout configuration, but editing payout defaults or line overrides is not implemented yet
* No localization implementation yet
* Prediction UI is functional but still basic; it does not yet show settled outcomes
* No tests yet
* Repository is not initialized as a Git repo yet

## Next Recommended Step

Continue Sprint 6 hardening and prepare Sprint 7 settlement:

1. Add event result storage needed by settlement
2. Add clearer market option rules and validation before settlement
3. Add settlement service tests before wiring automatic settlement
4. Add startup health checks for MariaDB connectivity
5. Add a dedicated deployment-time migration workflow before production
