# Virtual External Provider Plan

Last updated: 2026-06-03

## Starting the Test Provider

Run only the virtual provider service:

```powershell
dotnet run --project apps/test-provider/PoolPredict.TestProvider.csproj
```

Run the virtual provider together with the PoolPredict API and web app:

```powershell
.\start_services.ps1 -WithTestProvider
```

The provider listens on:

```text
http://localhost:5090
```

Swagger UI is available in Development:

```text
http://localhost:5090/swagger
```

To make PoolPredict consume the virtual provider, configure:

```json
{
  "EventProvider": {
    "Provider": "VirtualProvider",
    "VirtualProvider": {
      "BaseUrl": "http://localhost:5090"
    }
  }
}
```

## Purpose

PoolPredict needs a realistic testing provider that is separate from the API process. This provider should let us test external-provider synchronization without waiting for real tournaments or depending on FootballData availability.

The provider should be hosted as its own service and expose HTTP endpoints that PoolPredict consumes through a normal provider adapter.

## Goals

* Test real HTTP provider synchronization.
* Test tournament, participant and event import from an external system.
* Test provider-side updates for kickoff time, event status and scores.
* Test PoolPredict sync idempotency.
* Test provider-managed versus manually managed event behavior.
* Keep all data clearly marked as test data.
* Keep settlement manual in PoolPredict.

## Proposed Service

Add a standalone provider service:

```text
apps/test-provider
```

Recommended name:

```text
Virtual Sports Data Provider
```

PoolPredict provider identity:

```text
VirtualProvider
```

The service can run locally first and later be deployed anywhere.

Recommended initial implementation:

* .NET minimal API, to match the existing repository stack.
* JSON-file-backed mutable state for easy reset and inspection.
* Swagger/OpenAPI enabled in Development so test data can be inspected and edited from Swagger UI.

Recommended Swagger packages:

```text
Swashbuckle.AspNetCore
```

Recommended minimal API setup:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(...);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

## PoolPredict Configuration

PoolPredict should support a new provider adapter:

```text
VirtualProviderEventProvider
```

Development config example:

```json
{
  "EventProvider": {
    "Provider": "VirtualProvider"
  },
  "VirtualProvider": {
    "BaseUrl": "http://localhost:5090"
  }
}
```

## Swagger UI

The virtual provider should expose Swagger UI for manual testing:

```text
http://localhost:5090/swagger
```

Swagger should include both provider read endpoints and test-control endpoints. This gives a lightweight admin surface for changing provider-side data without building a separate UI first.

Recommended Swagger behavior:

* Enable Swagger only in Development by default.
* Group read endpoints under `Provider`.
* Group mutation endpoints under `Test control`.
* Include clear request/response examples for event updates.
* Show enum values for event statuses.
* Return the updated event from every event mutation endpoint so the change is visible immediately in Swagger UI.

Recommended event status enum:

```text
Scheduled
Live
Finished
Postponed
Cancelled
```

## Test Data

The provider should return one tournament:

```text
Virtual WC 2026
```

Suggested data:

* 16 virtual national teams.
* 12-24 events.
* 2-4 matches per day.
* Kickoff times starting from the current day.
* Stable external IDs.
* Test data flag or source metadata that PoolPredict can store as `IsTestData = true`.

Example external IDs:

```text
virtual-wc-2026
virtual-wc-2026-team-arg
virtual-wc-2026-event-001
```

Imported PoolPredict records should be provider-scoped:

* Tournament provider: `VirtualProvider`
* Participant provider: `VirtualProvider`
* Event provider: `VirtualProvider`
* Test data: `true`
* Default event management mode: `Provider`

## Provider Read API

PoolPredict should call these read endpoints:

```text
GET /health
GET /api/tournaments
GET /api/tournaments/{externalTournamentId}/participants
GET /api/tournaments/{externalTournamentId}/events
GET /api/tournaments/{externalTournamentId}/events?from={utc}&to={utc}
```

The event response should include:

* external event ID
* tournament external ID
* home participant external ID
* away participant external ID
* kickoff time
* status
* first-half home score
* first-half away score
* full-time home score
* full-time away score

## Provider Test-Control API

The provider should also expose test-control endpoints that mutate provider-side data. These endpoints are for local/manual testing and should not be treated as public sports-data APIs.

```text
GET  /api/test/events
GET  /api/test/events/{externalEventId}
PUT  /api/test/events/{externalEventId}
POST /api/test/events/{externalEventId}/start
POST /api/test/events/{externalEventId}/finish
POST /api/test/events/{externalEventId}/postpone
POST /api/test/events/{externalEventId}/cancel
POST /api/test/reset
```

Swagger UI should make these endpoints the primary way to edit provider data during manual testing.

Suggested endpoint behavior:

* `GET /api/test/events` returns all mutable events with current status and scores.
* `GET /api/test/events/{externalEventId}` returns one mutable event.
* `PUT /api/test/events/{externalEventId}` updates kickoff time, status and scores from a request body.
* `POST /api/test/events/{externalEventId}/start` sets status to `Live`.
* `POST /api/test/events/{externalEventId}/finish` sets status to `Finished` and accepts final score fields.
* `POST /api/test/events/{externalEventId}/postpone` sets status to `Postponed` and optionally accepts a new kickoff time.
* `POST /api/test/events/{externalEventId}/cancel` sets status to `Cancelled`.
* `POST /api/test/reset` restores the original generated virtual tournament schedule.

Example update payload:

```json
{
  "startsAt": "2026-06-03T20:00:00Z",
  "status": "Finished",
  "firstHalfHomeScore": 1,
  "firstHalfAwayScore": 0,
  "fullTimeHomeScore": 2,
  "fullTimeAwayScore": 1
}
```

Example local command:

```powershell
Invoke-RestMethod `
  -Method Put `
  -Uri "http://localhost:5090/api/test/events/virtual-wc-2026-event-001" `
  -ContentType "application/json" `
  -Body '{
    "startsAt": "2026-06-03T20:00:00Z",
    "status": "Finished",
    "firstHalfHomeScore": 1,
    "firstHalfAwayScore": 0,
    "fullTimeHomeScore": 2,
    "fullTimeAwayScore": 1
  }'
```

## Expected PoolPredict Sync Behavior

Provider-managed events:

* PoolPredict imports new provider events.
* PoolPredict updates kickoff time when the provider changes it.
* PoolPredict updates event status when the provider changes it.
* PoolPredict stores provider scores when the provider returns them.
* Repeated syncs should not duplicate tournaments, participants or events.

Manually managed events:

* PlatformAdmin can switch an event from `Provider` to `Manual`.
* Provider sync must skip manually managed events completely.
* Provider changes to kickoff, status or score must not overwrite manual PoolPredict values.
* PlatformAdmin can switch an event back to `Provider` if needed.

Settlement:

* Provider score sync does not automatically settle predictions.
* PlatformAdmin still starts settlement manually from `/admin/settlement`.
* Re-settlement remains a PlatformAdmin action.

## Manual Test Matrix

### Initial Provider Import

1. Run the virtual provider.
2. Open the virtual provider Swagger UI at `http://localhost:5090/swagger`.
3. Configure PoolPredict with `EventProvider:Provider = VirtualProvider`.
4. Start PoolPredict API and web.
5. Open `/admin/provider`.
6. Select `VirtualProvider`.
7. Sync selected provider.
8. Confirm `Virtual WC 2026`, participants and events appear.

Expected result:

* Provider status shows imported counts.
* Admin event list shows `VirtualProvider` and test data labels.
* Normal pool creation can select `Virtual WC 2026`.

### Provider Event Update

1. Pick an imported event.
2. Update the event in virtual provider Swagger using `PUT /api/test/events/{externalEventId}`.
3. Sync provider again from PoolPredict.
4. Open `/admin/events`.

Expected result:

* Kickoff time, status and scores update in PoolPredict.
* Provider remains `VirtualProvider`.
* Event remains provider-managed.

### Manual Mode Skip

1. Pick an imported event.
2. In PoolPredict `/admin/events`, switch it to `Manual`.
3. Change the same event in the virtual provider.
4. Sync provider again.

Expected result:

* PoolPredict does not overwrite kickoff time, status or scores.
* The event remains manually managed.

### Full Pool Flow

1. Sync `Virtual WC 2026`.
2. Create a pool using the virtual tournament.
3. Submit predictions before kickoff.
4. Update one provider event to `Finished` with scores.
5. Sync provider.
6. Manually settle the event from `/admin/settlement`.
7. Review prediction history and leaderboard.

Expected result:

* Markets generate from local payout defaults.
* Predictions lock by event kickoff.
* Settlement uses PoolPredict's stored/snapshotted market data.
* Leaderboard updates after manual settlement.

### Re-Settlement

1. Settle an event once.
2. Change the provider event score.
3. Sync provider again while the event is provider-managed.
4. Manually settle the same event again.

Expected result:

* PoolPredict writes correction ledger entries instead of duplicate payouts.
* Prediction history and leaderboard reflect the corrected result.

## Future Enhancements

* Add a small UI page in the test provider if Swagger becomes too limited for event editing.
* Add Docker Compose support for the virtual provider.
* Add scripted scenario presets:
  * all scheduled
  * one live match
  * one finished match
  * postponed match
  * cancelled match
* Add provider failure simulations:
  * 401 unauthorized
  * 500 server error
  * slow response
  * invalid payload
* Add PoolPredict automated integration tests against the provider contract.
