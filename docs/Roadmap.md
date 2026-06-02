# DOCUMENT 3 - IMPLEMENTATION ROADMAP

# Current Progress

See `docs/ImplementationStatus.md` for the latest implementation handoff notes.

Current status:

* Sprint 0 through Sprint 10 are implemented for the current MVP scope
* API and web apps have working auth, pool management, tournament browsing and admin provider sync
* MariaDB persistence uses EF Core migrations when configured
* The web app has prediction entry and PlatformAdmin manual settlement flows
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
* Basic market option input for Winner, Handicap, Over/Under, Odd/Even and Correct Score

Moved to later sprints:

* Outcome display after settlement
* Deeper per-market option validation
* Event result storage moved to Sprint 7 and is implemented

---

# Sprint 7

## Goal

Manual MVP Settlement Engine

## Deliverables

Winner Settlement

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
* Winner, Handicap, Over/Under, Odd/Even and Correct Score settlement
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

* Winner settlement tests
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

* Settlement calculator tests for Winner, Handicap, Over/Under, Odd/Even and Correct Score
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

# Sprint 12

## Goal

Production Readiness

## Deliverables

Audit Logs

Rate Limiting

Error Handling

Monitoring

Database Backup

Localization admin screens

Translation fallback checks

Acceptance:

Production deployment ready.

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
* Tournament winner predictions
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
