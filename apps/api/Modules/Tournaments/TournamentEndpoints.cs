namespace PoolPredict.Api.Modules.Tournaments;

using System.Security.Claims;

public static class TournamentEndpoints
{
    public static IEndpointRouteBuilder MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tournaments");

        group.MapGet("/", (TournamentCatalog catalog) => Results.Ok(catalog.GetTournaments()));

        group.MapGet("/{tournamentId:guid}/events", (Guid tournamentId, TournamentCatalog catalog) =>
        {
            return catalog.GetTournament(tournamentId) is null
                ? Results.NotFound()
                : Results.Ok(catalog.GetEvents(tournamentId));
        });

        group.MapGet("/{tournamentId:guid}/participants", (Guid tournamentId, TournamentCatalog catalog) =>
        {
            return catalog.GetTournament(tournamentId) is null
                ? Results.NotFound()
                : Results.Ok(catalog.GetParticipants(tournamentId));
        });

        group.MapGet("/provider/status", (TournamentCatalog catalog) =>
            Results.Ok(catalog.GetProviderStatus()));

        group.MapPost("/sync", async (ClaimsPrincipal principal, TournamentSyncJob syncJob, CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(await syncJob.ExecuteAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization();

        return app;
    }
}
