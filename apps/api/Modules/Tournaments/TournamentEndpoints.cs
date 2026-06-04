using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Tournaments;
using PoolPredict.Api.Infrastructure.Persistence;
using System.Security.Claims;

namespace PoolPredict.Api.Modules.Tournaments;

public static class TournamentEndpoints
{
    public static IEndpointRouteBuilder MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tournaments");

        group.MapGet("/", (TournamentCatalog catalog) => Results.Ok(catalog.GetTournamentResponses()));

        group.MapGet("/{tournamentId:guid}/events", async (
            Guid tournamentId,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var tournamentExists = await db.Tournaments.AnyAsync(tournament => tournament.Id == tournamentId, cancellationToken);
            if (!tournamentExists)
            {
                return Results.NotFound();
            }

            var events = await db.Events
                .AsNoTracking()
                .Where(matchEvent => matchEvent.TournamentId == tournamentId)
                .GroupJoin(
                    db.EventResults.AsNoTracking(),
                    matchEvent => matchEvent.Id,
                    result => result.EventId,
                    (matchEvent, results) => new { MatchEvent = matchEvent, Result = results.FirstOrDefault() })
                .OrderBy(item => item.MatchEvent.StartsAt)
                .Select(item => new EventResponse(
                    item.MatchEvent.Id,
                    item.MatchEvent.TournamentId,
                    item.MatchEvent.HomeParticipantId,
                    item.MatchEvent.AwayParticipantId,
                    item.MatchEvent.HomeParticipant,
                    item.MatchEvent.AwayParticipant,
                    item.MatchEvent.StartsAt,
                    item.MatchEvent.Status,
                    item.MatchEvent.Provider,
                    item.MatchEvent.IsTestData,
                    item.MatchEvent.ManagementMode,
                    item.Result == null ? null : item.Result.FirstHalfHomeScore,
                    item.Result == null ? null : item.Result.FirstHalfAwayScore,
                    item.Result == null ? null : item.Result.FullTimeHomeScore,
                    item.Result == null ? null : item.Result.FullTimeAwayScore,
                    item.Result == null ? null : item.Result.RecordedAt))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(events);
        });

        group.MapGet("/{tournamentId:guid}/participants", (Guid tournamentId, TournamentCatalog catalog) =>
        {
            return catalog.GetTournament(tournamentId) is null
                ? Results.NotFound()
                : Results.Ok(catalog.GetParticipants(tournamentId));
        });

        group.MapGet("/provider/status", (TournamentCatalog catalog) =>
            Results.Ok(catalog.GetProviderStatus()));

        group.MapGet("/providers", (TournamentCatalog catalog) =>
            Results.Ok(catalog.GetProviderList()));

        group.MapGet("/events/admin", async (
            ClaimsPrincipal principal,
            string? provider,
            bool? isTestData,
            EventManagementMode? managementMode,
            EventStatus? status,
            Guid? tournamentId,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = from matchEvent in db.Events.AsNoTracking()
                        join tournament in db.Tournaments.AsNoTracking()
                            on matchEvent.TournamentId equals tournament.Id
                        join result in db.EventResults.AsNoTracking()
                            on matchEvent.Id equals result.EventId into resultGroup
                        from eventResult in resultGroup.DefaultIfEmpty()
                        select new { MatchEvent = matchEvent, Tournament = tournament, Result = eventResult };

            if (!string.IsNullOrWhiteSpace(provider))
            {
                query = query.Where(item => item.MatchEvent.Provider == provider);
            }

            if (isTestData is not null)
            {
                query = query.Where(item => item.MatchEvent.IsTestData == isTestData);
            }

            if (managementMode is not null)
            {
                query = query.Where(item => item.MatchEvent.ManagementMode == managementMode);
            }

            if (status is not null)
            {
                query = query.Where(item => item.MatchEvent.Status == status);
            }

            if (tournamentId is not null)
            {
                query = query.Where(item => item.MatchEvent.TournamentId == tournamentId);
            }

            var events = await query
                .OrderBy(item => item.MatchEvent.StartsAt)
                .Take(200)
                .Select(item => new AdminEventResponse(
                    item.MatchEvent.Id,
                    item.MatchEvent.TournamentId,
                    item.Tournament.Name,
                    item.MatchEvent.HomeParticipant,
                    item.MatchEvent.AwayParticipant,
                    item.MatchEvent.StartsAt,
                    item.MatchEvent.Status,
                    item.MatchEvent.Provider,
                    item.MatchEvent.IsTestData,
                    item.MatchEvent.ManagementMode,
                    item.Result == null ? null : item.Result.FirstHalfHomeScore,
                    item.Result == null ? null : item.Result.FirstHalfAwayScore,
                    item.Result == null ? null : item.Result.FullTimeHomeScore,
                    item.Result == null ? null : item.Result.FullTimeAwayScore,
                    item.Result == null ? null : item.Result.RecordedAt))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(events);
        }).RequireAuthorization();

        group.MapPost("/sync", async (ClaimsPrincipal principal, string? provider, TournamentSyncJob syncJob, CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(await syncJob.ExecuteAsync(provider, cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPut("/events/{eventId:guid}/management-mode", async (
            Guid eventId,
            ClaimsPrincipal principal,
            SetEventManagementModeRequest request,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            TournamentCatalog catalog,
            CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var matchEvent = await db.Events.SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
            if (matchEvent is null)
            {
                return Results.NotFound();
            }

            matchEvent.ManagementMode = request.ManagementMode;
            await db.SaveChangesAsync(cancellationToken);
            catalog.SetEventState(matchEvent.Id, matchEvent.StartsAt, matchEvent.Status, matchEvent.ManagementMode);
            return Results.Ok(new { eventId, matchEvent.ManagementMode });
        }).RequireAuthorization();

        group.MapPut("/events/{eventId:guid}/manual", async (
            Guid eventId,
            ClaimsPrincipal principal,
            UpdateManualEventRequest request,
            IDbContextFactory<PoolPredictDbContext> dbContextFactory,
            TournamentCatalog catalog,
            CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var matchEvent = await db.Events.SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
            if (matchEvent is null)
            {
                return Results.NotFound();
            }

            matchEvent.ManagementMode = EventManagementMode.Manual;
            matchEvent.StartsAt = request.StartsAt;
            matchEvent.Status = request.Status;

            if (request.FullTimeHomeScore is not null && request.FullTimeAwayScore is not null)
            {
                var result = await db.EventResults.SingleOrDefaultAsync(candidate => candidate.EventId == eventId, cancellationToken);
                if (result is null)
                {
                    result = new PersistedEventResult
                    {
                        Id = Domain.Common.Ids.NewId(),
                        EventId = eventId
                    };
                    db.EventResults.Add(result);
                }

                result.FullTimeHomeScore = request.FullTimeHomeScore.Value;
                result.FullTimeAwayScore = request.FullTimeAwayScore.Value;
                result.FirstHalfHomeScore = request.FirstHalfHomeScore;
                result.FirstHalfAwayScore = request.FirstHalfAwayScore;
                result.RecordedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            catalog.SetEventState(matchEvent.Id, matchEvent.StartsAt, matchEvent.Status, matchEvent.ManagementMode);
            return Results.Ok(new { eventId, matchEvent.ManagementMode, matchEvent.Status, matchEvent.StartsAt });
        }).RequireAuthorization();

        return app;
    }
}
