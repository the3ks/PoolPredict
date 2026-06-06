# Implementation Status

Last updated: 2026-06-05

## Current State

The project has an initial MVP foundation using a DDD-lite structure.

The web app now includes Sprint 1 authentication and Sprint 2 pool management screens backed by authenticated API endpoints. Authentication and pool management are separated into dedicated routes under the planned app structure.

Sprint 3 tournament infrastructure is implemented with participant records, an event provider abstraction and a mock World Cup provider. Sprint 4 external integration is implemented with configurable provider selection, a FootballData provider adapter and an admin-triggered tournament sync path. Sprint 5 market and settlement foundation is implemented with DB-backed payout defaults, profile-based market generation and settlement run/log tables. Sprint 6 prediction submission is implemented with authenticated prediction entry, member balances and prediction history. Sprint 7 settlement is implemented for manual admin settlement, event result storage, re-settlement correction entries and provider/manual event management modes. Sprint 8 settlement hardening is implemented with calculator-level and service-level automated tests, selected-option validation, cancelled-event settlement and quarter-line handicap support. Sprint 9 admin event management is implemented with event browse/filter, manual event editing and selected-event settlement from the admin UI. Sprint 10 leaderboards and settled prediction display are implemented from persisted prediction, market and point-ledger read models. Sprint 11 user/admin route reorganization, pool discovery and identity/admin security is implemented with root-route normal user flows, a dedicated `/admin` panel, pool discovery, join-request approval, email verification, password recovery, admin user management and SMTP settings. Sprint 12 market profile, 1X2, pool-detail UX and admin event-list polish is implemented with 1X2 settlement, option-level market prediction summaries, wider 1X2/Handicap market rows, mobile prediction slip, Standard/Casual profile refinements, PlatformAdmin-only Expert pool creation and closed-event admin list cleanup. Sprint 13 profile personalization and pool stake controls is implemented with editable display name/avatar, pool cover images and server-enforced stake rules. Sprint 14 pool-detail UX and deployment polish is implemented with current-balance summary display, compact avatar-backed leaderboard identity, numeric formatting across pool pages, restored overview collapse defaults and documented SQL-script deployment workflow. MariaDB-backed EF Core persistence is now required for the API.

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
* Added dedicated `docs/Requirements-1X2.md`
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
* `POST /api/auth/verify-email`
* `POST /api/auth/resend-verification`
* `POST /api/auth/forgot-password`
* `POST /api/auth/reset-password`
* `POST /api/auth/change-password`
* `PUT /api/auth/me`
* `GET /api/admin/users`
* `POST /api/admin/users/{userId}/password-reset`
* `POST /api/admin/users/{userId}/verify-email`
* `GET /api/admin/email-settings`
* `PUT /api/admin/email-settings`
* `POST /api/admin/email-settings/test`
* `GET /api/admin/pools`
* `GET /api/tournaments`
* `GET /api/tournaments/{tournamentId}/events`
* `GET /api/tournaments/{tournamentId}/participants`
* `GET /api/tournaments/provider/status`
* `GET /api/tournaments/events/admin`
* `POST /api/tournaments/sync`
* `PUT /api/tournaments/events/{eventId}/management-mode`
* `PUT /api/tournaments/events/{eventId}/manual`
* `GET /api/markets/payout-configurations`
* `POST /api/pools`
* `GET /api/pools`
* `GET /api/pools/discover`
* `GET /api/pools/{poolId}`
* `PUT /api/pools/{poolId}`
* `POST /api/pools/{poolId}/invites`
* `POST /api/pools/{poolId}/join-requests`
* `GET /api/pools/{poolId}/join-requests`
* `POST /api/pools/{poolId}/join-requests/{requestId}/approve`
* `POST /api/pools/{poolId}/join-requests/{requestId}/deny`
* `GET /api/pools/invites/{code}`
* `POST /api/pools/join`
* `GET /api/pools/{poolId}/markets`
* `POST /api/predictions`
* `GET /api/predictions/pool/{poolId}`
* `GET /api/predictions/pool/{poolId}/market-summaries`
* `GET /api/predictions/pool/{poolId}/leaderboard`
* `GET /api/predictions/balance?poolId={poolId}`
* `POST /api/settlement/events/{eventId}/result`

Implemented domain slice backed by MariaDB persistence:

* Identity users
* User external logins
* Identity verification and password reset tokens
* SMTP email settings
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

Current provider data when the Mock provider is selected:

* FIFA World Cup 2026
* Six mock participants
* Three placeholder events

Tournament behavior:

* Event provider selection is configurable through `EventProvider:Provider`
* `TournamentCatalog` loads from persistence first
* If no persisted tournament data exists, the catalog syncs from `IEventProvider`
* `MockEventProvider` provides World Cup 2026 tournament, participants and events
* `FootballDataProvider` can import teams and matches from football-data.org when configured
* `VirtualProviderEventProvider` can import tournaments, participants and events from the standalone virtual provider service when configured
* `TournamentSyncJob` supports admin-triggered provider sync
* PlatformAdmin users can select which configured provider to sync from `/admin/provider`
* `FootballData` sync is explicit from Admin and does not auto-import on empty database startup
* Provider data is saved to MariaDB
* Provider/source metadata is stored for tournaments, participants and events
* Provider external IDs are scoped by provider so Mock and FootballData data cannot overwrite each other

Current Casual pool behavior:

* Creates Fulltime-only 1X2, Over/Under, Odd/Even and Correct Score markets
* Uses fixed point payout defaults from the active payout configuration

Current Standard pool behavior:

* Creates full-time and first-half markets
* Generates 1X2, Handicap, Over/Under, Odd/Even and Correct Score markets
* Generates market lines and payout multipliers from the active payout configuration
* Winner markets are intentionally excluded because they require volatile outcome-specific payouts
* Expert profile pool creation is guarded so only PlatformAdmin users can create Expert pools

Market configuration behavior:

* Seeds DB-backed MVP global payout defaults when no payout configuration exists
* Supports Casual, Standard and Expert profile rule sets
* Stores payout configuration versions and market rules in MariaDB when configured
* Exposes payout defaults to PlatformAdmin users from `GET /api/markets/payout-configurations`
* Handicap markets are generated as `LinePending`
* PlatformAdmin can confirm full-time and first-half handicap lines per event from Admin Event Management
* Handicap predictions require a confirmed line and open only inside the 24-hour pre-kickoff window

Settlement foundation behavior:

* Adds persisted settlement run records for future idempotent event settlement
* Adds persisted settlement log records linked to settlement runs
* Stores event results in `event_results`
* Supports admin-triggered settlement from a recorded event result
* Settles 1X2, Handicap, Over/Under, Odd/Even and Correct Score predictions
* Validates selected options for settled markets
* Supports quarter-line handicap half-win and half-lose outcomes
* Supports cancelled-event settlement as stake refunds and market voiding
* Re-settlement compares expected credit with existing credit and writes correction ledger entries when a result changes
* Marks manually settled events as manually managed
* Persists event management mode and skips manually managed events during provider sync

Prediction behavior:

* Prediction endpoints require authentication
* Prediction submission derives the pool member from the signed-in user
* Deducts stake immediately
* Adds starting balance ledger entry for first member action
* Blocks new predictions when member balance is negative
* Enforces pool-level minimum, maximum and per-event stake caps
* Blocks prediction submission after the market event start time
* Validates Correct Score prediction input as `score_number-score_number` with zero-or-positive integer scores
* Snapshots:
  * market type
  * market period
  * line value
  * payout multiplier
  * payout configuration version

Authentication behavior:

* Registers email/password users
* Email/password registration now returns a verification-required message instead of issuing a JWT immediately
* Sends email verification links when SMTP is enabled
* Blocks email/password login until the user's email is verified
* Supports verification email resend
* Supports forgot-password reset links
* Supports reset-password token consumption
* Supports signed-in password changes from the profile page
* Supports signed-in display-name and avatar URL updates from the profile page
* Hashes passwords with PBKDF2-SHA256
* Stores only hashed verification and reset tokens
* Issues signed JWT bearer tokens
* Validates bearer tokens for protected profile access
* Supports an MVP Google-login endpoint that links a Google provider subject to a user account
* Treats Google-linked accounts as email verified
* Seeds a development-only PlatformAdmin from `apps/api/appsettings.Development.json`
* Marks the configured seeded PlatformAdmin email as verified if it already exists
* Stores identity data in MariaDB

Admin identity and email behavior:

* PlatformAdmin users can list and search registered users
* PlatformAdmin users can reset a user's password to a temporary password
* Temporary password reset marks the user as requiring a password change
* PlatformAdmin users can mark a user's email as verified
* PlatformAdmin users can view and update SMTP settings
* SMTP settings default to AWS SES SMTP shape
* PlatformAdmin users can send a test email
* SMTP password is not returned by the API; the settings response exposes only whether a saved password exists

Pool behavior:

* Pool creation requires an authenticated user
* Pool creator is recorded as the owner member
* Authenticated users can list only pools they belong to
* Authenticated users can discover pools they do not belong to
* Authenticated users can create a persisted pending join request for pools they do not belong to
* Pool owners/admins can list pending and decided join requests
* Pool owners/admins can approve join requests, which adds the requester as a pool member
* Pool owners/admins can deny join requests
* Pool owners/admins can edit pool name and starting balance
* Pool owners/admins can configure a cover image URL
* Pool owners/admins can configure default stake, min stake, max stake and per-event total stake cap
* Pool owners/admins can create invite codes
* Authenticated users can join pools by invite code
* Pool summaries include member and invite counts
* Pool join requests are filtered to pending requests in the owner/admin pool-detail controls

Sprint 12 market and pool UX behavior:

* 1X2 markets render as a full-width three-option row on the pool detail Markets card
* Handicap markets render as a full-width two-option row on the pool detail Markets card
* Handicap option boxes show team label, FT/HT score context and handicap line
* Over/Under, Odd/Even and Correct Score cards show option-level prediction users
* Standard profile cards show FT/HT period labels where the profile includes multiple periods
* Pool market display limits scheduled matches to the configured upcoming window
* Recently finished, settled, cancelled and postponed matches are grouped in a collapsible recent-closed panel
* Recent closed match visibility uses a 24-hour pool-detail display window
* Pool Summary and Leaderboard share the top row on pool detail pages
* Owner/admin settings and pending join requests share a collapsible row
* Prediction history page shows My predictions before Leaderboard
* Mobile pool detail uses a bottom sticky prediction slip so prediction entry remains reachable while browsing markets
* Header navigation labels use Home and Pools with icons, and the brand uses a soccer-ball mark
* Participant codes are exposed on event responses and used by the frontend to render flags when mapped
* Admin Event Management and Settlement sort Settled/Cancelled events to the bottom and hide them 72 hours after kickoff

### Web

Added a Next.js app shell at `apps/web`.

Current web behavior:

* Shows running/upcoming tournaments publicly on `/`
* Shows tournament provider/source labels on public and signed-in tournament cards
* Provides local-user login and registration forms on `/login` and `/register`
* Provides password show/hide controls on password entry forms
* Registration shows a verification-required result instead of immediately signing in the user
* Provides `/verify-email` for verification links
* Allows users to resend verification email
* Provides `/forgot-password` for reset-link requests
* Provides `/reset-password` for email password resets
* Keeps Google login hidden from the web UI for now
* Persists the JWT access token in browser local storage
* Loads the signed-in profile from `GET /api/auth/me`
* Provides a root-route normal user shell under `/`, `/pools/*` and `/profile`
* Shows running/upcoming tournaments on the signed-in dashboard
* Loads tournaments for pool creation
* Creates pools for the signed-in user on `/pools/new`
* Lists the signed-in user's owned/joined pools and other available pools on `/pools`
* Allows signed-in users to request joining another pool from `/pools`
* Shows pool overview and editable owner/admin settings on `/pools/[poolId]`
* Allows pool owners/admins to approve or deny join requests on `/pools/[poolId]`
* Creates invite codes for owner/admin pools on `/pools/[poolId]/invites`
* Joins pools by invite code on `/pools/join`
* Shows signed-in profile details on `/profile`
* Allows signed-in users to edit display name and avatar URL on `/profile`
* Allows signed-in users to change password on `/profile`
* Provides a dedicated PlatformAdmin panel under `/admin`
* Shows all pools across all users on `/admin/pools`
* Shows provider status and selected-provider sync on `/admin/provider` for PlatformAdmin users
* Allows PlatformAdmin users to browse and filter events by provider/source, management mode and status on `/admin/events`
* Allows PlatformAdmin users to edit event kickoff, status, mode and stored scores on `/admin/events`
* Sorts settled/cancelled admin events to the bottom and hides them after the 72-hour closed-event window
* Allows PlatformAdmin users to switch events between provider-managed and manually managed mode on `/admin/events`
* Allows PlatformAdmin users to settle or cancel-settle a selected event on `/admin/settlement`
* Shows active payout defaults on `/admin/payout` for PlatformAdmin users
* Allows PlatformAdmin users to search users and reset passwords on `/admin/users`
* Allows PlatformAdmin users to mark user email as verified on `/admin/users`
* Allows PlatformAdmin users to configure SMTP settings and send a test email on `/admin/system`
* Shows pool markets grouped by match on `/pools/[poolId]`
* Allows signed-in pool members to submit predictions from available markets
* Shows pool cover image and compact stake-rule summary on `/pools/[poolId]`
* Prefills pool default stake and shows per-pool stake limits in the prediction form
* Shows current member balance and enriched prediction history with settlement outcome and net points on the pool page
* Shows pool leaderboard rows with balance, win rate, ROI and prediction counts on the pool page
* Shows avatar-backed leaderboard identity rows and localized numeric formatting across pool list, pool detail and prediction history pages
* Restores Summary & Leaderboard collapse/expand on `/pools/[poolId]` with desktop-expanded and mobile-collapsed defaults
* Links to API health
* Supports persisted dark/light theme switching across public, auth and signed-in app routes
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
* `identity_tokens`
* `email_settings`
* `tournaments`
* `participants`
* `events`
* `event_results`
* `pools`
* `pool_members`
* `pool_invites`
* `pool_join_requests`
* `markets`
* `predictions`
* `point_ledger`
* `payout_configurations`
* `payout_configuration_market_rules`
* `settlement_runs`
* `settlement_logs`
* EF Core initial migration
* EF migration `20260602125442_PoolJoinRequests`
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
* Standard profile generates the configured MVP market set
* API build passes after Sprint 5 migration
* Web production build passes after Sprint 5 admin UI update
* Prediction submission deducts points
* Balance endpoint returns updated balance
* API build passes after Sprint 6 authenticated prediction changes
* Web production build passes after Sprint 6 prediction UI update
* API build passes after Sprint 7 event result and settlement changes
* Web production build passes after Sprint 7 admin settlement UI update
* API build passes after Sprint 7 provider/source scoping
* Web production build passes after Sprint 7 provider/source labels
* API build passes after Sprint 7 event management mode and re-settlement correction updates
* `dotnet test PoolPredict.sln` passes after Sprint 8 settlement hardening
* API build, web production build and `dotnet test PoolPredict.sln` pass after Sprint 9 admin event management
* API build, web production build and `dotnet test PoolPredict.sln` pass after Sprint 10 leaderboards and settled prediction display
* EF migration `20260602080731_IdentityEmailAndPasswordManagement` was generated and applied locally
* `dotnet test PoolPredict.sln -c Release` and web production build pass after Sprint 11 route reorganization, pool join-request and identity/admin security updates
* EF migration `20260602125442_PoolJoinRequests` was generated
* API build and web production build pass after Sprint 12 1X2, market UX, profile restriction and admin event-list updates

## Known Gaps

* Shared environments should review generated migration scripts before applying them
* API startup fails when `ConnectionStrings:MariaDb` is missing
* Google login API does not yet validate a real Google ID token or OAuth client configuration and is not exposed in the web UI
* SMTP password is stored in application database storage without dedicated encryption-at-rest beyond database-level protections
* Email verification and reset email delivery require SMTP settings to be configured and enabled
* Public auth token flows do not yet include throttling/rate limiting
* Pool member management is limited to owner/member roles, invite-code joins and join-request approval
* Join request approval is shown on pool detail pages only; there is no dedicated member-management page yet
* Admin all-pools view is read-only
* Join-request notification/email delivery is not implemented yet
* Invite links are represented as invite codes; full URL routing is not implemented yet
* FootballData sync requires an API token and has not been smoke-tested against a real provider account
* Tournament sync is manually triggered from admin UI; recurring background scheduling is not implemented yet
* Settlement is admin-triggered by design; automatic settlement is not supported for MVP
* Admin event-management list is functional but intentionally basic; pagination and text search are not implemented yet
* Admin UI displays payout configuration, but editing global payout defaults is not implemented yet
* Existing pools do not automatically receive newly added 1X2 markets without an explicit backfill or regeneration action
* Participant flag rendering depends on mapped provider participant codes; unmapped codes fall back to plain names
* Mobile prediction slip and market-row interactions do not yet have automated UI tests
* Avatar and pool cover editing are URL-based only; direct file upload/storage is not implemented yet
* Leaderboard is read-only and derived from current point ledger; historical leaderboard snapshots are not implemented yet
* No localization implementation yet
* Prediction UI is functional but still basic; deeper result visualizations can be improved later
* Test coverage is focused on settlement; broader API and UI coverage is not implemented yet

## Next Recommended Step

Proceed to Sprint 15 production readiness:

1. Add audit logging for sensitive admin and auth actions
2. Add request throttling/rate limiting for public auth endpoints
3. Harden production error handling and monitoring coverage
4. Verify backup/restore operational steps as part of release readiness
5. Verify localization fallback behavior for the supported UI scope
6. Leave AI recap as later optional enhancement in Sprint 16
