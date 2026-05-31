# Web App Structure Plan

Last updated: 2026-05-31

## Problem

The current MVP web page combines authentication, profile display and pool management on one screen. This was useful for early smoke testing, but it is not the right product structure.

Authentication should be a focused entry flow. Once signed in, users should land in an application shell with navigation, pool context and task-specific pages.

## Goals

* Separate public/auth pages from signed-in app pages
* Make pool ownership and membership workflows easy to understand
* Keep the first screen after login useful for repeat users
* Support future prediction, leaderboard, settlement and recap pages without redesigning navigation
* Keep user-facing text translation-ready later

## Recommended Sitemap

```text
/
/login
/register
/auth/google-callback

/app
/app/pools
/app/pools/new
/app/pools/join
/app/pools/[poolId]
/app/pools/[poolId]/settings
/app/pools/[poolId]/invites
/app/pools/[poolId]/members
/app/pools/[poolId]/events
/app/pools/[poolId]/markets
/app/pools/[poolId]/predictions
/app/pools/[poolId]/leaderboard
/app/pools/[poolId]/recaps

/app/profile
/app/admin
/app/admin/payouts
/app/admin/tournaments
/app/admin/translations
/app/admin/audit
```

## Page Responsibilities

### Public Pages

`/`

Redirect signed-in users to `/app`. Redirect anonymous users to `/login`.

This should not become a marketing landing page during MVP unless product goals change.

`/login`

Email/password login and Google login entry point.

No pool forms on this page.

`/register`

Account creation.

After successful registration, redirect to `/app/pools` or `/app/pools/new`.

`/auth/google-callback`

Future page for real Google OAuth callback handling.

The current MVP Google subject form should be removed once real Google validation is implemented.

### Signed-In App Pages

`/app`

Default signed-in dashboard.

Recommended behavior:

* If the user has pools, show recent pools and upcoming events.
* If the user has no pools, show direct actions for creating or joining a pool.

`/app/pools`

Pool list.

Show owned and joined pools with role, member count, profile and next event.

Primary actions:

* Create pool
* Join pool

`/app/pools/new`

Pool creation wizard.

MVP steps:

1. Select tournament
2. Select profile
3. Set name and starting balance
4. Confirm generated market summary

After creation, redirect to `/app/pools/[poolId]`.

`/app/pools/join`

Join by invite code or invite link.

After joining, redirect to `/app/pools/[poolId]`.

`/app/pools/[poolId]`

Pool overview.

Show:

* Pool identity and role
* Upcoming events
* Prediction status
* Current balance
* Leaderboard preview
* Latest recap when available

`/app/pools/[poolId]/events`

Fixture list for the selected pool's tournament.

Each event should link to available markets and predictions.

`/app/pools/[poolId]/markets`

Generated market list grouped by event and period.

For MVP, this can be read-only until prediction UI is complete.

`/app/pools/[poolId]/predictions`

Prediction submission and prediction history.

This becomes the main Sprint 6 page.

`/app/pools/[poolId]/leaderboard`

Pool rankings and member stats.

This becomes the main Sprint 9 page.

`/app/pools/[poolId]/recaps`

Weekly AI recap history.

This becomes the main Sprint 11 page.

`/app/pools/[poolId]/settings`

Owner/admin pool settings.

MVP editable fields:

* Pool name
* Starting balance

Profile and tournament should remain locked after creation unless a later requirement says otherwise.

`/app/pools/[poolId]/invites`

Owner/admin invite management.

MVP:

* Create invite code
* Copy invite code
* Show generated invite history

Later:

* Expiration
* Revocation
* Invite link routing

`/app/pools/[poolId]/members`

Owner/admin member management.

MVP can be read-only if role management is not ready.

Later:

* Promote pool admin
* Remove member
* Transfer ownership

### Profile Page

`/app/profile`

Signed-in user profile and session controls.

Show:

* Display name
* Email
* Platform role
* Sign out

### Admin Pages

Admin pages should be hidden unless the user has the `PlatformAdmin` role.

`/app/admin`

Admin landing page.

`/app/admin/payouts`

Global point payout configuration.

`/app/admin/tournaments`

Tournament sync and provider status.

`/app/admin/translations`

Localization term management.

`/app/admin/audit`

Admin action audit log.

## Layout Recommendation

### Auth Layout

Used by:

* `/login`
* `/register`
* `/auth/google-callback`

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

### App Layout

Used by all `/app/*` routes.

Desktop structure:

```text
Top bar
  Product name
  Current pool switcher
  Profile menu

Sidebar
  Dashboard
  Pools
  Events
  Predictions
  Leaderboard
  Recaps
  Settings
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
  Menu button
  Profile menu

Drawer navigation
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

Platform admin:

```text
Dashboard
Pools
Admin
Profile
```

Admin expands into:

```text
Payouts
Tournaments
Translations
Audit
```

## Recommended Next UI Sprint

Split the current single page into a proper route structure:

1. Move login/register into `/login` and `/register`.
2. Add an app shell under `/app`.
3. Move pool list/create/join into `/app/pools`, `/app/pools/new` and `/app/pools/join`.
4. Add `/app/pools/[poolId]` overview.
5. Keep invite creation in `/app/pools/[poolId]/invites`.
6. Keep profile and sign-out in `/app/profile` or a profile menu.

Acceptance:

* Anonymous users see auth pages only.
* Signed-in users see the app shell only.
* Pool management is not mixed with login forms.
* Existing Sprint 2 API functionality remains usable from the UI.
