# Handicap Line Requirements

## Goal

Handicap markets use fixed payout multipliers, but their line values must be set per match because match strength varies. Winner markets are excluded from the platform, so handicap line management is the main market-specific pricing control.

## Ownership

* Platform Admin owns handicap line values.
* Pool owners cannot set or edit handicap line values.
* Global payout configuration provides default handicap lines for market creation only.
* Per-match handicap lines are confirmed from the admin event workflow.

## Market Scope

Supported handicap markets:

* Full-time Handicap
* First-half Handicap

The stored `lineValue` is always from the home participant perspective.

Example:

* `lineValue = -0.5` means Home `-0.5` / Away `+0.5`
* `lineValue = +0.25` means Home `+0.25` / Away `-0.25`

## Lifecycle

Handicap markets are generated as `LinePending`.

Handicap market states:

* `LinePending`: market exists, line is not confirmed for play, predictions are blocked.
* `Open`: Platform Admin confirmed the line, but predictions are still only allowed inside the open window.
* `Locked`: kickoff reached; predictions are blocked.
* `Settled`: manual settlement completed.
* `Voided`: cancelled-event settlement/refund completed.

Non-handicap markets may be generated as `Open`.

## Opening Window

Default handicap open window:

```text
24 hours before kickoff
```

The API setting is:

```json
"Markets": {
  "HandicapOpenWindowHours": 24
}
```

Prediction submission for handicap markets is allowed only when all conditions are true:

* market status is `Open`
* current time is within 24 hours before kickoff
* kickoff has not passed
* member has sufficient eligibility under normal pool prediction rules

If a Platform Admin confirms a line earlier than 24 hours before kickoff, the market can show its line but prediction submission must remain blocked until the 24-hour window begins.

## Practical Line Values

Full-time handicap:

* Common range: `-2.5` to `+2.5`
* Step: `0.25`
* Default template: `+0.5`

First-half handicap:

* Common range: `-1.5` to `+1.5`
* Step: `0.25`
* Default template: `+0.25` or `+0.5`

Quarter lines are supported because settlement already supports half-win and half-lose outcomes.

## Admin UI

Admin event management should show handicap line controls for the selected match:

* Full-time handicap line
* First-half handicap line
* Current status for each period
* Confirm/update line action

Confirming a line applies to all pool markets for that event and period.

## Settlement

Submitted predictions snapshot the line value at submission time. Re-settlement must use the snapshotted line value, not the current market line.
