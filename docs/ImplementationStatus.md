# Implementation Status

Last updated: 2026-05-31

## Current State

The project has an initial MVP foundation using a DDD-lite structure.

The web app now includes Sprint 1 authentication and Sprint 2 pool management screens backed by authenticated API endpoints. Authentication and pool management are separated into dedicated routes under the planned app structure.

Sprint 3 tournament infrastructure is now implemented with participant records, an event provider abstraction and a mock World Cup provider. MariaDB-backed EF Core persistence is available for the current MVP data model when a connection string is configured.

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
* `POST /api/auth/register`
* `POST /api/auth/login`
* `POST /api/auth/google`
* `GET /api/auth/me`
* `GET /api/tournaments`
* `GET /api/tournaments/{tournamentId}/events`
* `GET /api/tournaments/{tournamentId}/participants`
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
* `GET /api/predictions/balance?poolId={poolId}&memberId={memberId}`

Implemented in-memory domain slice:

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

* `TournamentCatalog` loads from persistence first when MariaDB is configured
* If no persisted tournament data exists, the catalog syncs from `IEventProvider`
* `MockEventProvider` provides World Cup 2026 tournament, participants and events
* Provider data is saved to MariaDB when `ConnectionStrings:MariaDb` is configured
* Without a MariaDB connection string, the catalog falls back to in-memory provider data

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

Authentication behavior:

* Registers email/password users
* Hashes passwords with PBKDF2-SHA256
* Issues signed JWT bearer tokens
* Validates bearer tokens for protected profile access
* Supports an MVP Google-login endpoint that links a Google provider subject to a user account
* Seeds a development-only PlatformAdmin from `apps/api/appsettings.Development.json`
* Stores identity data in MariaDB when `ConnectionStrings:MariaDb` is configured
* Falls back to in-memory identity storage when no connection string is configured

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

* Redirects `/` to `/login` or `/app` based on local session state
* Provides login and registration forms on `/login` and `/register`
* Provides an MVP Google-login form
* Persists the JWT access token in browser local storage
* Loads the signed-in profile from `GET /api/auth/me`
* Provides a signed-in app shell under `/app`
* Loads tournaments for pool creation
* Creates pools for the signed-in user on `/app/pools/new`
* Lists the signed-in user's pools on `/app/pools`
* Shows pool overview and editable owner/admin settings on `/app/pools/[poolId]`
* Creates invite codes for owner/admin pools on `/app/pools/[poolId]/invites`
* Joins pools by invite code on `/app/pools/join`
* Shows signed-in profile details on `/app/profile`
* Links to API health
* Does not yet provide working prediction UI

### Infrastructure

Added Docker Compose with:

* MariaDB
* Redis
* API
* Web
* Caddy

Added optional API persistence foundation:

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
* EF Core initial migration
* Automatic startup `Database.Migrate()` when MariaDB is configured

Current recommendation:

* Run API and web directly for local testing
* Use local MariaDB or Docker MariaDB for persistence testing
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
* Prediction submission deducts points
* Balance endpoint returns updated balance

## Known Gaps

* Production deployment should eventually run migrations as a controlled deployment step instead of at API startup
* Stores fall back to in-memory behavior when no MariaDB connection string is configured
* Google login does not yet validate a real Google ID token or OAuth client configuration
* Pool member management is limited to owner/member roles and invite-code joins
* Invite links are represented as invite codes; full URL routing is not implemented yet
* No settlement engine yet
* No admin UI for payout configuration or line overrides yet
* No localization implementation yet
* Web UI has auth and pool forms but no working prediction forms yet
* No tests yet
* Repository is not initialized as a Git repo yet

## Next Recommended Step

Continue persistence hardening:

1. Add automated persistence tests
2. Add startup health checks for MariaDB connectivity
3. Add admin-visible provider sync status
4. Confirm indexes against query patterns as features mature
5. Move migration execution out of API startup before production

After that, move to Sprint 4 external integration.
