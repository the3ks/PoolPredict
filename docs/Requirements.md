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
7. Platform automatically settles results.
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

### Pools

* Create Pool
* Join Pool
* Invite Link
* Pool Settings

### Tournament

MVP Tournament:

* FIFA World Cup 2026

### Markets

Fulltime:

* Winner
* Handicap
* Over/Under
* Odd/Even
* Correct Score

First Half:

* Winner
* Handicap
* Over/Under
* Odd/Even
* Correct Score

### Point Payout Configuration

* Do not use public betting odds
* External providers do not provide payout values for MVP
* Platform Admin manages global default point payout configuration
* Global defaults define market line values, payout multipliers and supported line sets
* Tournament-specific payout configuration may be added later
* When a new match is created, markets are generated from the current global defaults
* Before a market is locked, Platform Admin can change line values for that match's markets
* Pool owners cannot manually manage line values or payout configuration
* Submitted predictions must snapshot the market line value and payout configuration at submit time
* Settlement must use the snapshotted line value and payout configuration recorded on the prediction

### Predictions

* Submit prediction
* Lock before kickoff
* View history
* Deduct points immediately when a prediction is submitted
* Prevent new predictions when member balance is negative

### Points

* Virtual points only
* Configurable starting balance
* Prediction points are deducted at submit time
* Negative balances are allowed
* Members with negative balances cannot submit additional predictions until their balance is positive again

### Settlement

* Automatic settlement is required for MVP
* Settlement must support all MVP markets
* Settlement must update member balances from the point ledger
* Settlement reruns must not duplicate payouts, refunds or deductions

### Leaderboards

* Individual ranking
* Pool ranking

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
