# DOCUMENT 1 - PRODUCT REQUIREMENTS DOCUMENT (PRD)

## Product Name

PoolPredict

## Vision

A SaaS platform that allows anyone to create a private or public prediction pool around real-world tournaments.

Examples:

* FIFA World Cup
* UEFA Euro
* Champions League
* Premier League
* Tennis tournaments
* Pickleball tournaments
* Esports tournaments

The platform is NOT a betting platform.

Users predict outcomes using virtual points only.

No real money.

No deposit.

No withdrawal.

No gambling functionality.

## Core User Journey

Pool Owner:

1. Sign up.
2. Create a Pool.
3. Select Tournament.
4. Select Profile (Casual / Standard / Expert).
5. Invite Members.
6. Platform automatically creates markets.
7. Platform Admin verifies event results and manually settles completed events.
8. Platform automatically generates recap.

Pool Member:

1. Join Pool.
2. View upcoming events.
3. Submit predictions.
4. Earn points.
5. Climb leaderboard.
6. Read AI recap.

## Key Design Principle

Pool Owners should not act as bookmakers.

Pool Owners should not:

* Create fixtures.
* Enter scores.
* Manage line values or payout configuration manually.
* Settle results manually.

Goal:

Create Pool → Invite Members → Done.

## MVP Features

### Authentication

* Email login
* Google login
* Registration and password forms should provide show/hide password controls where password entry benefits from it
* Email/password registration must require email verification before login
* Users must be able to resend verification email
* Users must be able to request a password reset link by email
* Password reset must be completed through an email link
* Signed-in users must be able to change their password from profile settings

### Admin User and Email Management

* Platform Admin can view and search registered users.
* Platform Admin can reset a user's password.
* Admin password reset must not expose the user's existing password.
* Reset users should be required to change their password after receiving a temporary password.
* Platform Admin can mark a user's email as verified.
* Platform Admin can configure SMTP settings used by system emails.
* SMTP configuration should support AWS SES SMTP first.
* SMTP settings should include:
  * provider
  * host
  * port
  * username
  * password
  * from email
  * from name
  * STARTTLS setting
  * enabled/disabled state
* Platform Admin can send a test email to validate SMTP settings.
* Email verification and password reset emails must use the configured SMTP sender when enabled.

### Pools

* Create Pool
* Join Pool by invite code
* Discover other pools
* Request to join another pool
* Pool owner/admin can approve or deny join requests
* Invite Link
* Pool Settings

Pool route behavior:

* Normal users should be able to use the root route family for their main workflows:
  * `/`
  * `/pools`
  * `/pools/new`
  * `/pools/join`
  * `/pools/[poolId]`
  * `/profile`
* `/pools` should show pools the signed-in user owns or joined first, then other available pools.
* Other pool rows should show basic information and a request-to-join action.
* Pool detail pages should show join requests to pool owners/admins.
* Pool owner/admin controls on the pool detail page should show only pending join requests by default.
* The pool owner/admin settings and pending join requests area should be collapsible.
* The owner/admin controls area should expand by default when pending join requests exist and collapse by default when none exist.
* Approving a join request should add the requester as a pool member.
* Denying a join request should keep the requester outside the pool.
* Pool owner/admin can configure a pool background cover image.
* Pool detail summary should show compact pool settings, including stake-rule values.
* Pool detail summary should show the signed-in member's current balance.
* The Summary & Leaderboard row on the pool detail page should be collapsible, expanded by default on desktop and collapsed by default on mobile.
* Normal users must not be able to create Expert profile pools.
* Expert profile pool creation is restricted to Platform Admin users.
* The web app should not expose `/app` as a product route family; `apps/web/app` is only the Next.js App Router source directory.

### Admin Panel

* Platform Admin work should live under `/admin`.
* Only Platform Admin users can access `/admin`.
* Admin navigation should use a flat sidebar, not a nested Admin group.
* Admin sidebar pages should include:
  * Pools
  * Tournament provider
  * Event management
  * Settlement
  * Payout
  * User management
  * System settings
* Admin Pools should show all pools across users with basic owner, tournament, provider, member, invite and balance information.
* Tournament provider admin should allow PlatformAdmin users to select and sync any configured provider.

### Tournament

MVP Tournament:

* FIFA World Cup 2026

### Event Management

* The platform must support provider-managed events and manually managed events.
* Event management modes are required for mock testing, manual tournaments, provider outage recovery, result correction and settlement verification.
* Each event must have a management mode:
  * `provider`
  * `manual`
* The default event management mode is `provider`.
* Provider-managed events are synchronized from the configured external provider when synchronization is enabled.
* Provider synchronization may update:
  * kickoff time
  * event status
  * first-half result
  * full-time result
* Manually managed events are controlled by Platform Admin.
* Provider synchronization must skip manually managed events completely.
* Platform Admin can edit manually managed event fields:
  * kickoff time
  * event status
  * first-half home score
  * first-half away score
  * full-time home score
  * full-time away score
* Platform Admin can switch an event between provider-managed and manually managed modes at any time.
* Admin Event Management and Settlement event lists should sort `Settled` and `Cancelled` events below active/manageable events.
* Admin Event Management and Settlement event lists should hide `Settled` and `Cancelled` events once kickoff time is more than 72 hours ago.

### Parallel Provider Testing

* The platform should support mock provider data and real provider data in the same environment for testing alongside production-like data.
* Mock data must be clearly marked as test data.
* Provider-originated records must be scoped by provider to avoid external ID collisions.
* Unique provider keys should include provider identity, for example:
  * tournaments: `(provider, external_id)`
  * participants: `(tournament_id, provider, external_id)`
  * events: `(tournament_id, provider, external_id)`
* Mock provider records must not overwrite real provider records.
* Real provider sync must not overwrite mock provider records.
* Admin UI should make provider/source visible when browsing tournaments and events.
* Test data should be easy to filter out, hide from normal users, or delete.

### Markets

Detailed `1X2` rules are maintained in `docs/Requirements-1X2.md`.

Detailed handicap line rules are maintained in `docs/Requirements-Handicap.md`.

Profile behavior:

* Casual profile includes Fulltime-only 1X2, Over/Under, Odd/Even and Correct Score markets.
* Standard profile includes 1X2 plus Fulltime and First Half Handicap, Over/Under, Odd/Even and Correct Score markets.
* Expert profile is reserved for Platform Admin-created pools.

Fulltime:

* 1X2
* Handicap
* Over/Under
* Odd/Even
* Correct Score

First Half:

* Handicap
* Over/Under
* Odd/Even
* Correct Score

### Point Payout Configuration

* Do not use public betting odds
* External providers do not provide payout values for MVP
* 1X2 uses a fixed point payout multiplier instead of volatile outcome-specific public odds
* 1X2 uses a `2.5x` point payout multiplier (`1:1.5` payout rate)
* MVP markets use fixed point payout multipliers
* Platform Admin manages global default point payout configuration
* Global defaults define market line values, payout multipliers and supported line sets
* Tournament-specific payout configuration may be added later
* When a new match is created, markets are generated from the current global defaults
* Before a market is locked, Platform Admin can change match-specific handicap line values
* Pool owners cannot manually manage line values or payout configuration
* Submitted predictions must snapshot the market line value and payout configuration at submit time
* Settlement must use the snapshotted line value and payout configuration recorded on the prediction

### Predictions

* Submit prediction
* Lock before kickoff
* View history
* Deduct points immediately when a prediction is submitted
* Prevent new predictions when member balance is negative
* Each pool must define:
  * default stake
  * minimum stake per prediction
  * maximum stake per prediction
  * maximum total stake per event
* Prediction submit UI must guide the user with the pool's current stake limits.
* Prediction submit UI should show remaining per-event allowance for the selected event when practical.
* Prediction API must enforce all pool stake rules server-side.
* Correct Score input must use `score_number-score_number` format.
* Correct Score score numbers must be zero or positive integers.
* Pool members can choose only one 1X2 option per event: home win, draw or away win.
* Pool members can add more points to the same 1X2 option for the same event as long as normal stake and event-cap rules allow it.
* Pool market displays should show current prediction users grouped by selected market option.
* Market lists should show only scheduled matches that kick off inside the configured upcoming display window.
* Recently closed matches should remain visible only inside the configured recent closed display window.
* On mobile, the prediction form should remain accessible as a bottom sticky prediction slip without hiding the final market rows.

### Points

* Virtual points only
* Configurable starting balance
* Prediction points are deducted at submit time
* Negative balances are allowed
* Members with negative balances cannot submit additional predictions until their balance is positive again

### Profile

* Signed-in users can update their display name.
* Signed-in users can update their avatar.
* MVP avatar editing may use a URL-based image reference instead of file upload/storage.

### Settlement

* Automatic settlement is not supported for MVP.
* Settlement is always initiated manually by Platform Admin.
* Settlement must support 1X2, Handicap, Over/Under, Odd/Even and Correct Score markets
* Settlement must update member balances from the point ledger
* Settlement reruns must not duplicate payouts, refunds or deductions
* Platform Admin can execute re-settlement for an event.

### Leaderboards

* Individual ranking
* Pool ranking
* Pool detail pages should show the leaderboard beside the summary on larger screens.
* Prediction history pages should show the member's prediction history before the leaderboard.
* Leaderboard rows should support member avatar display with a compact rank/avatar/name identity layout.
* Pool-related balance, stake and limit values should use readable numeric separators in the UI.

### AI

* Weekly Pool Recap

### Localization

* English is the default and initially supported language
* All user-facing terms must be translation-ready
* Platform admins can add another language later by entering translations for each English term
* Missing translations fall back to English

### Audit

* Track all admin actions

## Out of Scope

* Real money
* Payment
* Playable games
* Card games
* Mobile app
* Public betting odds
* Extra-time betting
* Penalty betting
