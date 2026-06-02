# UI Standards

Last updated: 2026-06-02

## Purpose

This document defines the baseline UI rules for PoolPredict. Future human or AI-assisted UI work should follow these rules unless a product decision explicitly changes the direction.

## Stack

* Next.js App Router
* React
* TypeScript
* Plain global CSS in `apps/web/app/styles.css`
* Local UI primitives in `apps/web/app/components/ui.tsx`
* `lucide-react` for icons

Do not add a full UI framework unless there is a clear implementation need.

## Theme

PoolPredict supports dark and light themes.

Rules:

* Keep dark as the default theme.
* Use the shared `ThemeToggle` component for switching.
* Persist the selected theme in `localStorage` with the `poolpredict-theme` key.
* Apply theme colors through `:root` and `:root[data-theme="light"]` tokens.
* Use semantic CSS variables from `:root` instead of hardcoded component colors.
* Preserve strong contrast for body text, form fields, buttons and status text.
* Keep accent usage focused on actions, active states, icons and important status surfaces.

## Layout

Public pages:

* Keep `/` as a public tournament dashboard, not a marketing landing page.
* The first viewport should clearly show the PoolPredict brand and the tournament browsing purpose.
* Keep public cards scannable and action-oriented.

Authenticated pages:

* Use the app shell under `/app`.
* Use `PageHeader` for page titles and primary actions.
* Use `Panel` for bounded task surfaces.
* Use `StatusPill` for compact status, loading and count messages.
* Use `StatGrid` for compact numeric summaries.

## Components

Prefer the local primitives:

* `PageHeader`
* `Panel`
* `StatusPill`
* `StatGrid`
* `IconLabel`

Rules:

* Keep components small and app-specific.
* Do not introduce generic abstractions unless multiple screens need them.
* Preserve existing route behavior when doing visual-only work.
* Do not nest cards inside cards.
* Keep cards at `8px` radius or less.

## Icons

Use `lucide-react` icons for:

* Navigation items
* Button labels
* Page headers
* Status pills
* Stat tiles
* Repeated data cards when the icon improves scanning

Rules:

* Icons should support the label, not replace important text.
* Use `aria-hidden="true"` for decorative/supporting icons.
* Prefer `IconLabel` inside buttons and links.
* Keep icon sizes consistent with existing usage.

## Copy

* Keep UI text short and direct.
* Avoid instructional text that explains obvious controls.
* Keep user-facing strings translation-ready by avoiding string concatenation in display copy where practical.
* Use public-user language on `/`, `/login` and `/register`.
* Use operational language inside `/app`.

## Performance

* Keep the frontend fast and dependency-light.
* Avoid client-side libraries for effects, animation or styling unless needed.
* Prefer CSS for visual polish.
* Avoid large image or media dependencies until the product needs them.

## Verification

For UI changes:

* Run `npm run build` from `apps/web`.
* Check desktop and mobile layouts for obvious overflow.
* Confirm buttons, links, forms and disabled states remain readable in both themes.
