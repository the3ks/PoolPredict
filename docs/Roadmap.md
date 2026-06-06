# DOCUMENT 3 - IMPLEMENTATION ROADMAP

# Current Progress

See `docs/ImplementationStatus.md` for the latest implementation handoff notes.

Current status:

* Sprint 0 through Sprint 14 are implemented for the current MVP scope
* API and web apps have working auth, identity security flows, pool management, pool discovery/join requests, tournament browsing and admin provider sync
* MariaDB persistence uses EF Core migrations when configured
* The web app has prediction entry, enhanced market displays and PlatformAdmin manual settlement flows
* Automatic settlement is not part of the MVP path

---

# Development Strategy

Build vertical slices.

Do not build all layers first.

Each sprint must produce working functionality.

Roadmap alignment rules:

* Completed sprint sections should describe implemented behavior only.
* Uncompleted items must be moved into an unimplemented future sprint.
* Implemented behavior that does not match the latest requirements should be listed as a fix in a later sprint, not hidden inside a completed sprint.
* MVP settlement is manual PlatformAdmin settlement only. Automatic settlement is not planned for MVP.

---

# Sprint 0

## Goal

Project bootstrap

## Deliverables

Repository structure:

```text
/apps
    /web
    /api

/docker

/docs
```

Infrastructure:

* Docker Compose
* MariaDB
* Redis
* Caddy

Backend:

* .NET 10 API skeleton
* Localization key structure

Frontend:

* Next.js skeleton
* Translation-ready UI foundation

Acceptance:

Application starts locally with one command.

---

# Sprint 1

## Goal

Identity & Authentication

## Deliverables

Backend:

* ASP.NET Identity
* JWT Authentication
* Google Login

Frontend:

* Login
* Registration
* Profile

Acceptance:

User can login and persist session.

---

# Sprint 2

## Goal

Pool Management

## Deliverables

Features:

* Create Pool
* Edit Pool
* Join Pool
* Invite Links
* Member Management

Tables:

* pools
* pool_members
* pool_invites

Acceptance:

Users can create and join pools.

---

# Sprint 3

## Goal

Tournament Infrastructure

## Deliverables

Entities:

* tournaments
* participants
* events

Provider abstraction:

* IEventProvider

Providers:

* MockProvider

Acceptance:

World Cup tournament displayed from mock data.

---

# Sprint 4

## Goal

External Integration

## Deliverables

FootballDataProvider

Jobs:

* TournamentSyncJob

Sync behavior:

* Provider sync imports provider-owned tournaments, participants and events.
* Provider sync is manually triggered by PlatformAdmin.

Web:

* Public running/upcoming tournament dashboard

Acceptance:

World Cup fixtures can be imported from the configured provider by a PlatformAdmin.

Anonymous users can browse running and upcoming tournaments before signing in.

---

# Sprint 5

## Goal

Market Engine & Basic Settlement Foundation

## Deliverables

Market Profiles:

* Casual
* Standard
* Expert

Market Generation:

* Auto generate markets
* Auto publish markets
* Generate market line values from global point payout defaults

Settlement Foundation:

* Market lifecycle
* Event lifecycle
* Point ledger model
* Idempotent settlement run model
* Point payout configuration model
* Prediction snapshot model

Acceptance:

Pool owner selects profile and markets appear automatically.

All MVP markets have lifecycle records and settlement-ready metadata.

## Current Implementation Notes

Implemented:

* DB-backed MVP global payout defaults
* Casual, Standard and Expert payout rule sets
* Market generation from active payout defaults
* PlatformAdmin payout defaults view on `/admin/payout`
* Settlement run and settlement log persistence tables

Moved to later implemented sprints:

* Prediction-facing market entry UI
* Event result storage
* Settlement calculation and idempotent rerun service

---

# Sprint 6

## Goal

Predictions

## Deliverables

Features:

* Submit prediction
* Prediction history
* Deduct points on submit
* Block new predictions when member balance is negative
* Snapshot line value and payout configuration at submit time

Acceptance:

Users can submit predictions before lock.

## Current Implementation Notes

Implemented:

* Authenticated prediction submission
* Member balance lookup for the signed-in pool member
* Prediction history for the signed-in pool member
* Pool page market picker grouped by match
* Basic market option input for Handicap, Over/Under, Odd/Even and Correct Score

Moved to later sprints:

* Outcome display after settlement
* Deeper per-market option validation
* Event result storage moved to Sprint 7 and is implemented

---

# Sprint 7

## Goal

Manual MVP Settlement Engine

## Deliverables

Handicap Settlement

Over/Under Settlement

Odd/Even Settlement

Correct Score Settlement

Push and refund handling

Idempotent settlement reruns

Acceptance:

PlatformAdmin can record an event result and manually settle all predictions for that event.

Re-settlement does not duplicate payouts or refunds.

## Current Implementation Notes

Implemented:

* `event_results` persistence
* PlatformAdmin manual result entry and settlement trigger
* Handicap, Over/Under, Odd/Even and Correct Score settlement
* Settlement run/log records
* Idempotent rerun behavior that avoids duplicate payouts/refunds
* Re-settlement correction entries when event results change
* Provider/source metadata on tournaments, participants and events
* Provider-scoped external ID uniqueness for parallel Mock and real provider testing
* Event management mode persistence and admin mode switch endpoint
* Provider sync skip behavior for manually managed events
* Basic PlatformAdmin mode switch control on `/admin/events`

Moved to later sprints:

* Cancelled-event settlement, quarter-line handicap support and automated settlement tests moved to Sprint 8 and are implemented.
* Admin event browse/filter/edit workflow moved to Sprint 9 and is implemented.
* Prediction settled outcome display moved to Sprint 10.

---

# Sprint 8

## Goal

Settlement Hardening & Tests

## Deliverables

Automated tests:

* Handicap settlement tests
* Over/Under settlement tests
* Odd/Even settlement tests
* Correct Score settlement tests
* Push, cancelled-event and re-settlement correction tests

Support:

* 0
* 0.25
* 0.5
* 0.75
* 1.0
* 1.25
* 1.5

Validation:

* Deeper per-market selected-option validation
* Cancelled-event settlement fixes if current behavior does not match requirements
* Documented quarter-line handicap behavior
* Fix any implemented settlement behavior that does not match latest manual-settlement requirements

Acceptance:

Manual settlement and re-settlement calculations pass automated tests, including handicap edge cases, without requiring automatic settlement.

## Current Implementation Notes

Implemented:

* Settlement calculator tests for Handicap, Over/Under, Odd/Even and Correct Score
* Service-level tests for re-settlement correction deltas, idempotent reruns and cancelled-event refunds
* Selected-option validation for settlement
* Quarter-line handicap split settlement with half-win and half-lose outcomes
* Cancelled-event settlement refunds stake, voids markets and leaves the event cancelled

Moved to later sprints:

* Admin manual settlement UI cancelled-event control moved to Sprint 9 and is implemented.

---

# Sprint 9

## Goal

Admin Event Management

## Deliverables

Admin event browse/search for selecting events to manage and settle

Admin filters for:

* Real provider data
* Mock/test data
* Provider-managed events
* Manually managed events

PlatformAdmin can edit manually managed event fields:

* Kickoff time
* Event status
* First-half scores
* Full-time scores

Acceptance:

Pool owner only creates pool.

PlatformAdmin can browse, filter, edit and settle events without copying raw event IDs manually. Mock/test data remains clearly separated from real provider data.

## Current Implementation Notes

Implemented:

* Admin event list endpoint with provider, source, management mode, status and tournament filters
* Admin UI event filters for provider, real/mock-test source, provider/manual mode and status
* Selected-event editing for kickoff time, event status, management mode and stored scores
* Selected-event manual settlement without copying raw event IDs
* Cancelled-event settlement control in the admin UI
* Catalog state update after admin mode/status/kickoff edits so in-memory event reads do not drift immediately after manual edits

Known limitations to fix later:

* Event list currently caps results at 200 and does not implement pagination.
* Text search is not implemented yet.

---

# Sprint 10

## Goal

Leaderboards & Settled Prediction Display

## Deliverables

Views:

* Pool Ranking
* Member Ranking
* Settled prediction outcome display on pool pages

Statistics:

* Win Rate
* ROI
* Prediction Count

Acceptance:

Leaderboard updates automatically.

Members can see submitted predictions resolved as win, lose, push, cancelled or corrected after PlatformAdmin manual settlement.

Admin market line editing before lock is planned after leaderboard/result visibility.

## Current Implementation Notes

Implemented:

* Pool leaderboard read model derived from point ledger, members, users and predictions
* Leaderboard rows with balance, win rate, ROI, prediction count and settled prediction count
* Enriched member prediction history with market status, settlement outcome, settlement credit and net points
* Pool page leaderboard display
* Pool page settled prediction outcome display

Known limitations to fix later:

* Leaderboards are live read models only; historical snapshots are not implemented.
* Admin market line editing before lock remains unimplemented.

---

# Sprint 11

## Goal

User/Admin Route Reorganization, Pool Discovery & Identity Security

## Deliverables

Normal user route structure:

* Root-route user dashboard on `/`
* Pool list/create/join/detail routes under `/pools`
* Profile route on `/profile`
* No product route family under `/app`; `apps/web/app` is only the Next.js source directory

Admin route structure:

* Dedicated PlatformAdmin panel under `/admin`
* Flat admin sidebar
* Admin all-pools overview on `/admin/pools`
* Existing admin provider, event management, settlement, payout, user and system settings pages moved under `/admin/*`

Pool discovery:

* Users can see pools they own or joined first
* Users can see other available pools below their own pools
* Users can request to join another pool
* Pool owners/admins can view join requests
* Pool owners/admins can approve or deny join requests

Identity and admin security:

* Registration password show/hide toggle
* Email verification required before login
* Resend verification email flow
* Admin can mark user email as verified
* Forgot/reset password via email link
* Signed-in user password change
* Admin user search/list
* Admin user password reset
* Admin SMTP settings with AWS SES SMTP support

Acceptance:

Normal users can use `/`, `/pools/*` and `/profile` without entering the admin panel.

PlatformAdmin users can use `/admin/*` for admin work with a flat sidebar.

Pool owners/admins can approve a join request and the requester becomes a pool member.

Users must verify email before logging in, can recover passwords by email, and can change password while signed in.

PlatformAdmin users can manage users and SMTP settings from the admin panel.

## Current Implementation Notes

Implemented:

* Root-route user shell with tournament dashboard, pool navigation and profile access
* Dedicated `/admin` shell with flat sidebar
* `/admin/pools` read-only all-pools overview
* `/pools` split into `Your Pools` and `Other Pools`
* Persisted `pool_join_requests`
* Pool discovery endpoint for pools outside the signed-in user's membership
* Join request submit/list/approve/deny endpoints
* Pool detail join-request approval UI for pool owners/admins
* Registration password visibility toggle
* Email verification, resend verification, forgot-password and reset-password flows
* Signed-in password change on `/profile`
* Admin user list/search, admin password reset and admin email verification controls
* SMTP system settings with AWS SES SMTP-compatible configuration and test email

Known limitations to fix later:

* Join request approval is visible on the pool detail page only; there is no dedicated member-management page yet.
* Admin all-pools view is read-only.
* Join-request notification/email delivery is not implemented.
* SMTP password storage relies on database-level protections; dedicated application-layer encryption is not implemented.

---

# Sprint 12

## Goal

Market Profiles, 1X2, Pool Detail UX & Admin Event Polish

## Deliverables

Market profile behavior:

* Add Fulltime 1X2 market support.
* Include 1X2 in Casual and Standard profile defaults.
* Keep Casual profile Fulltime-only with 1X2, Over/Under, Odd/Even and Correct Score.
* Restrict Expert profile pool creation to PlatformAdmin users.

Pool detail market UX:

* Render 1X2 as a full-width three-option row.
* Render Handicap as a full-width two-option row.
* Show current option prediction users on market displays.
* Show Handicap period, line and FT/HT score context.
* Show Standard profile FT/HT labels on multi-period market cards.
* Show only upcoming scheduled matches inside the configured upcoming window.
* Keep recent closed matches collapsible and hide them after the recent closed window.
* Add mobile sticky prediction slip.
* Validate Correct Score input as zero-or-positive integer score pairs.

Pool detail summary and owner controls:

* Show Summary and Leaderboard side by side on larger screens.
* Move prediction history leaderboard below My predictions.
* Make owner settings and pending join requests collapsible.
* Show pending join requests only in the owner/admin pool detail panel.

Admin and event polish:

* Sort Settled and Cancelled events to the bottom on admin Event Management and Settlement screens.
* Hide Settled and Cancelled events more than 72 hours after kickoff.
* Add participant-code flag display support without database schema changes.
* Refresh header navigation labels and icons.

Acceptance:

Casual and Standard pools expose the intended market sets with clearer option-level prediction visibility.

Pool members can submit predictions efficiently on desktop and mobile.

PlatformAdmin event management lists stay focused on actionable events while still briefly showing recently closed matches.

## Current Implementation Notes

Implemented:

* `OneXTwo` market type, default payout rules and settlement calculation.
* 1X2 requirement document in `docs/Requirements-1X2.md`.
* Casual profile default rules with Fulltime-only 1X2, Over/Under, Odd/Even and Correct Score.
* Standard profile 1X2 support.
* PlatformAdmin-only Expert profile creation guard in API and web pool creation.
* Market prediction summary endpoint for option-level user lists.
* Pool detail 1X2 and Handicap wide-row market UI.
* Market display windows for upcoming scheduled and recent closed events.
* Summary/leaderboard pool detail layout and collapsible owner controls.
* Mobile sticky prediction slip for pool detail prediction entry.
* Correct Score format validation on prediction submit.
* Admin event list closed-event sorting and 72-hour closed-event hide window.
* Participant code enrichment on event responses and frontend flag rendering helper.
* Header navigation rename from Tournaments to Home, menu icons and soccer ball brand mark.

Known limitations to fix later:

* Existing pools do not automatically receive newly added 1X2 markets without an explicit backfill or regeneration action.
* Emoji/SVG flag rendering can vary by provider code coverage; unmapped participant codes fall back to plain participant names.
* Mobile prediction slip is CSS-driven and does not yet include full interaction tests.

---

# Sprint 13

## Goal

Profile Personalization & Pool Stake Controls

## Deliverables

Profile:

* Signed-in users can update display name.
* Signed-in users can update avatar URL.

Pool presentation:

* Pool owner/admin can set a pool cover image URL.
* Pool detail summary shows the cover and compact stake-rule summary.

Pool stake controls:

* Pool owner/admin can set:
  * default stake
  * minimum stake per prediction
  * maximum stake per prediction
  * maximum total stake per event
* Prediction submit UI must show stake hints and enforce those limits.
* API must enforce the same limits server-side.

Acceptance:

Users can personalize their profile.

Pool owners/admins can configure pool cover and stake rules.

Pool members can only submit predictions that satisfy the pool's configured stake limits.

## Current Implementation Notes

Implemented:

* Signed-in profile editing for display name and avatar URL on `/profile`
* Pool cover image URL in owner/admin settings on `/pools/[poolId]`
* Compact pool summary display for starting balance and stake rules
* Pool-level default stake, min stake, max stake and per-event cap persistence
* Prediction form guidance for default stake, range and per-event remaining allowance
* Server-side stake validation for per-prediction and per-event limits

Known limitations to fix later:

* Avatar and cover images are URL-based only; direct file upload/storage is not implemented.
* Pool list and dashboard cards do not yet reuse the cover image as a preview surface.

---

# Sprint 14

## Goal

Pool Detail UX & Deployment Polish

## Deliverables

Pool detail UX:

* Show the current signed-in member balance in the pool summary instead of only the pool starting balance.
* Show leaderboard identity with rank, avatar and display name in a compact single-line layout.
* Format displayed balances, stakes and pool limits with number separators across pool list, pool detail and prediction history pages.
* Restore collapse/expand for the Summary & Leaderboard row on the pool detail page.
* Default the Summary & Leaderboard row to expanded on desktop and collapsed on mobile.

Prediction and profile read model polish:

* Surface user avatar URLs into leaderboard responses so leaderboard rows can show member avatars consistently.

Deployment documentation:

* Document a production-safe option to generate EF migration SQL locally, review it, and apply it manually on production.
* Document the `FROM_MIGRATION` parameter pattern in both Windows PowerShell and shell-based deployment flows.

Acceptance:

Pool members can see their current balance and a compact leaderboard identity on the pool page without losing the summary/leaderboard collapse behavior.

Operators can choose either direct EF migration execution or locally generated SQL script deployment for production schema changes.

## Current Implementation Notes

Implemented:

* Pool summary now shows current member balance derived from the live leaderboard read model.
* Pool leaderboard and prediction-history leaderboard show avatar-backed identity rows with compact rank/avatar/name display.
* Pool-area numeric displays now use localized separators for balance, stake and limit values.
* Summary & Leaderboard collapse/expand was restored on `/pools/[poolId]`.
* Summary & Leaderboard now defaults to expanded on desktop and collapsed on mobile.
* Deployment guide now includes local SQL generation and manual production-apply workflows, including Windows `FROM_MIGRATION` input.

Known limitations to fix later:

* Leaderboard identity layout is still purely CSS-driven and does not yet have automated UI coverage.
* Avatar and cover images remain URL-based only; direct file upload/storage is not implemented.

---

# Sprint 15

## Goal

Production Readiness

## Deliverables

Audit logs:

* Persist audit logs for sensitive admin and auth actions.

Rate limiting:

* Add throttling/rate limiting for public auth endpoints.

Error handling:

* Harden production error handling and problem responses.

Monitoring and health:

* Add production-ready health and monitoring coverage for API and database connectivity.

Backup and restore verification:

* Verify backup and restore operational steps and make them part of the release workflow.

Localization fallback verification:

* Verify translation fallback behavior for the explicitly supported UI scope.

Acceptance:

Sensitive admin/auth actions are persisted in audit logs.

Public auth endpoints are throttled.

Production error responses are sanitized while logs remain diagnosable.

API health coverage includes application and database connectivity.

Backup and restore procedure is documented and verified.

Supported translated UI scope falls back to English correctly when a term is missing.

---

# Sprint 16

## Goal

AI Recap

## Deliverables

Weekly Recap Generation

Storage:

* pool_ai_reports

UI:

* Recap Page

Acceptance:

One recap generated per pool per week.

---

# MVP Release Criteria

A new user can:

1. Register
2. Create Pool
3. Select World Cup 2026
4. Select Standard Profile
5. Invite Friends
6. Submit Predictions
7. Have predictions settled by PlatformAdmin after event results are verified
8. View Leaderboard
9. Read Weekly Recap

Without Pool Owner acting as bookmaker or manually entering scores.

---

# Post-MVP Roadmap

Phase 2

* Additional football tournaments
* Better statistics

Phase 3

* Tennis
* Pickleball
* Esports

Phase 4

* SaaS subscriptions
* Organization plans
* White-label branding

Phase 5

* Future Playable Games Domain

Not in current scope.
