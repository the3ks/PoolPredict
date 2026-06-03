# Web App Structure Plan

Last updated: 2026-06-02

## Problem

The current MVP web page combines authentication, profile display and pool management on one screen. This was useful for early smoke testing, but it is not the right product structure.

Authentication should be a focused entry flow. Normal users should use the root route family with the same spacing as the public tournament dashboard. Platform admins should use a separate `/admin` panel for admin-only work.

## Goals

* Keep normal user functions available from the root route family
* Make pool ownership and membership workflows easy to understand
* Keep `/` useful for anonymous users and signed-in users
* Keep PlatformAdmin work in a dedicated `/admin` panel
* Support future prediction, leaderboard, settlement and recap pages without redesigning navigation
* Keep user-facing text translation-ready later

## Recommended Sitemap

Implementation note: `apps/web/app` is the Next.js App Router source directory. It does not mean the product should expose a `/app` URL route.

```text
/
/login
/register

/pools
/pools/new
/pools/join
/pools/[poolId]
/pools/[poolId]/settings
/pools/[poolId]/invites
/pools/[poolId]/members
/pools/[poolId]/events
/pools/[poolId]/markets
/pools/[poolId]/predictions
/pools/[poolId]/leaderboard
/pools/[poolId]/recaps

/profile

/admin
/admin/pools
/admin/provider
/admin/events
/admin/settlement
/admin/payout
/admin/users
/admin/system
```

## Page Responsibilities

### Public Pages

`/`

Public tournament dashboard.

Show running and upcoming tournaments to anonymous and signed-in users. Anonymous users can browse tournaments and then log in or register. Signed-in users can create pools, open their pools, access profile, and PlatformAdmin users can open the admin panel.

This should not become a marketing landing page during MVP unless product goals change.

`/login`

Local email/password login entry point.

No pool forms on this page.

`/register`

Account creation.

After successful registration, redirect to `/pools` or `/pools/new`.

Google login is not exposed in the web UI for now. If it returns later, add a real OAuth callback route and validated provider-token flow instead of the old development-only Google subject form.

### Normal User Pages

`/`

Default public and signed-in user dashboard.

Recommended behavior:

* Show running and upcoming tournaments.
* Anonymous users see login/register actions.
* Signed-in users see direct actions for pools and profile.
* PlatformAdmin users see a link to `/admin`.

`/pools`

Pool list.

Show owned and joined pools first with role, member count and profile. Show other pools below with basic owner, tournament, provider, profile, member and balance information plus a request-to-join action.

Primary actions:

* Create pool
* Join pool
* Request to join another pool

`/pools/new`

Pool creation wizard.

MVP steps:

1. Select tournament
2. Select profile
3. Set name and starting balance
4. Confirm generated market summary

After creation, redirect to `/pools/[poolId]`.

`/pools/join`

Join by invite code or invite link.

After joining, redirect to `/pools/[poolId]`.

`/pools/[poolId]`

Pool overview.

Show:

* Pool identity and role
* Upcoming events
* Prediction status
* Current balance
* Leaderboard preview
* Latest recap when available
* Join requests for pool owners/admins, with approve and deny actions

`/pools/[poolId]/events`

Fixture list for the selected pool's tournament.

Each event should link to available markets and predictions.

`/pools/[poolId]/markets`

Generated market list grouped by event and period.

For MVP, this can be read-only until prediction UI is complete.

`/pools/[poolId]/predictions`

Full prediction history for the signed-in pool member. Prediction submission remains on the pool detail page in the floating submit panel.

This becomes the main Sprint 6 page.

`/pools/[poolId]/leaderboard`

Pool rankings and member stats.

This becomes the main Sprint 9 page.

`/pools/[poolId]/recaps`

Weekly AI recap history.

This becomes the main Sprint 12 page.

`/pools/[poolId]/settings`

Owner/admin pool settings.

MVP editable fields:

* Pool name
* Starting balance

Profile and tournament should remain locked after creation unless a later requirement says otherwise.

`/pools/[poolId]/invites`

Owner/admin invite management.

MVP:

* Create invite code
* Copy invite code
* Show generated invite history

Later:

* Expiration
* Revocation
* Invite link routing

`/pools/[poolId]/members`

Owner/admin member management.

MVP can be read-only if role management is not ready.

Later:

* Promote pool admin
* Remove member
* Transfer ownership

### Profile Page

`/profile`

Signed-in user profile and session controls.

Show:

* Display name
* Email
* Platform role
* Sign out

### Admin Pages

Admin pages should be hidden unless the user has the `PlatformAdmin` role.

`/admin`

Admin landing page. It may redirect to `/admin/pools`.

`/admin/pools`

All pools across all users with basic PlatformAdmin summary information.

`/admin/provider`

Tournament provider status and provider sync.

`/admin/events`

Event browse, filtering, manual editing and provider/manual management mode changes.

`/admin/settlement`

Manual event settlement and cancelled-event settlement.

`/admin/payout`

Global point payout configuration.

`/admin/users`

Platform admin user search, email verification override and temporary password reset.

`/admin/system`

System settings. SMTP settings are the first implemented settings section.

## Layout Recommendation

Follow `docs/UIStandards.md` for theme behavior, local UI primitives and icon usage rules.

### Auth Layout

Used by:

* `/login`
* `/register`

Structure:

```text
Centered auth surface
  PoolPredict wordmark
  Form title
  Form fields
  Primary action
  Secondary auth link
```

Rules:

* No global app sidebar
* No pool management controls
* Keep the form narrow and focused

### User Layout

Used by `/`, `/pools/*` and `/profile`.

Desktop structure:

```text
Top bar
  Product name
  Tournaments
  Pools
  Profile
  Admin, only for PlatformAdmin

Main content
  Page title
  Page actions
  Task-specific content
```

Mobile structure:

```text
Top bar
  Product name
  Wrapped navigation/actions
Main content
```

### Pool Context

Most signed-in pages need an active pool context.

Recommended behavior:

* Store the last selected pool ID in local storage.
* If the selected pool is missing, choose the first joined pool.
* If the user has no pools, direct them to create or join a pool.
* Keep pool-specific navigation disabled until a pool is selected.

## Navigation Model

Primary navigation should be role-aware.

Pool member:

```text
Dashboard
Pools
Events
Predictions
Leaderboard
Recaps
Profile
```

Pool owner/admin:

```text
Dashboard
Pools
Events
Predictions
Leaderboard
Recaps
Members
Invites
Settings
Profile
```

Platform admin normal-user shell:

```text
Tournaments
Pools
Admin
Profile
```

### Admin Layout

Used by `/admin/*` routes.

Admin sidebar is flat:

```text
Pools
Tournament provider
Event management
Settlement
Payout
User management
System settings
```

## Implemented Route Split

The route split has been implemented:

1. Login/register are on `/login` and `/register`.
2. Normal user pages live under `/`, `/pools/*` and `/profile`.
3. Pool list/create/join are on `/pools`, `/pools/new` and `/pools/join`.
4. Pool overview is on `/pools/[poolId]`.
5. Invite creation is on `/pools/[poolId]/invites`.
6. Profile and password change are on `/profile`.
7. Platform admin functions live under `/admin/*` with a flat sidebar.

Acceptance:

* Anonymous users see auth pages only.
* Signed-in normal users can stay in the root route family.
* Pool management is not mixed with login forms.
* Admin functions are not mixed into one large page.
