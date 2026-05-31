# DOCUMENT 3 - IMPLEMENTATION ROADMAP

# Current Progress

See `docs/ImplementationStatus.md` for the latest implementation handoff notes.

Current status:

* Sprint 0 is partially complete
* API and web skeletons exist
* Docker Compose infrastructure exists
* A small in-memory API vertical slice exists for tournaments, pools, generated markets and prediction submission
* The web app is still a shell and has no working MVP forms yet

---

# Development Strategy

Build vertical slices.

Do not build all layers first.

Each sprint must produce working functionality.

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

Acceptance:

World Cup fixtures imported automatically.

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

---

# Sprint 7

## Goal

MVP Settlement Engine

## Deliverables

Winner Settlement

Handicap Settlement

Over/Under Settlement

Odd/Even Settlement

Correct Score Settlement

Push, refund and cancellation handling

Idempotent settlement reruns

Acceptance:

Automatic settlement works for completed MVP events.

---

# Sprint 8

## Goal

Handicap Hardening

## Deliverables

Support:

* 0
* 0.25
* 0.5
* 0.75
* 1.0
* 1.25
* 1.5

Unit Tests:

Minimum 50 test cases.

Acceptance:

All handicap calculations pass tests and edge cases are documented.

---

# Sprint 9

## Goal

Leaderboards

## Deliverables

Views:

* Pool Ranking
* Member Ranking

Statistics:

* Win Rate
* ROI
* Prediction Count

Acceptance:

Leaderboard updates automatically.

---

# Sprint 10

## Goal

Automation Hardening

## Deliverables

Auto Generate Markets

Auto Publish Markets

Auto Lock Markets

Auto Settle Results

Platform Admin can edit match market line values before lock

Acceptance:

Pool owner only creates pool.

No manual intervention required.

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
7. Have predictions automatically settled
8. View Leaderboard
9. Read Weekly Recap

Without any per-pool or per-match intervention from Platform Admin when global defaults are already configured.

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
