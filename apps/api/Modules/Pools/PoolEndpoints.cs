using PoolPredict.Api.Modules.Tournaments;

namespace PoolPredict.Api.Modules.Pools;

public static class PoolEndpoints
{
    public static IEndpointRouteBuilder MapPoolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pools");

        group.MapGet("/", (PoolStore pools) => Results.Ok(pools.GetPools()));

        group.MapPost("/", (CreatePoolRequest request, PoolStore pools, TournamentCatalog catalog) =>
        {
            try
            {
                var pool = pools.CreatePool(request, catalog);
                return Results.Created($"/api/pools/{pool.Id}", pool);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/{poolId:guid}/markets", (Guid poolId, PoolStore pools) =>
        {
            return pools.GetPool(poolId) is null
                ? Results.NotFound()
                : Results.Ok(pools.GetMarkets(poolId));
        });

        return app;
    }
}
