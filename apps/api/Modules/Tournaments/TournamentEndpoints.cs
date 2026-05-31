namespace PoolPredict.Api.Modules.Tournaments;

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

        return app;
    }
}
