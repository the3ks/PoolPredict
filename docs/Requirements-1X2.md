# 1X2 Market Requirements

## Market Scope

The `1X2` market is a full-time only match result market.

Supported selections:

* Home team to win
* Draw
* Away team to win

The market is available in the Casual profile. Standard and Expert profiles may also include it as a full-time market through the global payout defaults.

First-half, extra-time, penalty and aggregate-result variants are out of scope.

## Payout

All `1X2` selections use a fixed `2.5x` point payout multiplier (`1:1.5` payout rate).

The platform must not use public betting odds for this market.

## Prediction Rules

Users can submit a prediction by selecting exactly one of:

* The home participant name
* `Draw`
* The away participant name

For each pool member and event, only one `1X2` option choice is allowed.

After a member places a `1X2` prediction for an event, the member can place additional predictions on the same selected option as long as normal stake and event-cap rules allow it.

The member cannot place another `1X2` prediction for the same event on a different option.

Prediction submission must follow the standard pool and market locking rules:

* The pool must not have predictions locked.
* The market status must be `Open`.
* The event must be scheduled and not yet kicked off.
* The prediction stake must be deducted immediately.
* Submitted predictions must snapshot the payout multiplier and payout configuration version.

## Pool Detail Display

On the Pool details Markets card, `1X2` should be displayed as one full-width row for each match.

The row has three columns:

* Home team
* Draw
* Away team

Clicking one column prepares the Prediction form with the `1X2` market and selected option already selected.

Each column should show the pool members who placed predictions on that option when that information is available to the page.

## Settlement Rules

Settlement uses full-time scores only.

* Home selection wins when full-time home score is greater than full-time away score.
* Draw selection wins when full-time scores are equal.
* Away selection wins when full-time away score is greater than full-time home score.
* Non-winning selections lose.
* Cancelled events refund the stake and void the market using the standard cancelled-event settlement behavior.

## Casual Profile

The Casual profile should generate full-time markets only:

* `1X2` at `2.5x`
* Over/Under at `2x`
* Odd/Even at `2x`
* Correct Score at `5x`

Notes:
SQL Script to update payout multiplier of existing `1X2` markets:
```sql
START TRANSACTION;

UPDATE markets
SET payout_multiplier = 2.5
WHERE type = 'OneXTwo'
  AND status = 'Open';

UPDATE payout_configuration_market_rules
SET payout_multiplier = 2.5
WHERE market_type = 'OneXTwo'
  AND is_enabled = 1;

COMMIT;
```
