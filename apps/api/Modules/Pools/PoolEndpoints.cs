using PoolPredict.Api.Modules.Tournaments;
using System.Security.Claims;

namespace PoolPredict.Api.Modules.Pools;

public static class PoolEndpoints
{
    public static IEndpointRouteBuilder MapPoolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pools");

        group.MapGet("/", (ClaimsPrincipal principal, PoolStore pools) =>
        {
            return TryGetUserId(principal, out var userId)
                ? Results.Ok(pools.GetPoolsForUser(userId))
                : Results.Unauthorized();
        }).RequireAuthorization();

        group.MapPost("/", (ClaimsPrincipal principal, CreatePoolRequest request, PoolStore pools, TournamentCatalog catalog) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var pool = pools.CreatePool(userId, request, catalog);
                return Results.Created($"/api/pools/{pool.Id}", pool);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/{poolId:guid}", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var pool = pools.GetPoolForUser(poolId, userId);
            return pool is null ? Results.NotFound() : Results.Ok(pool);
        }).RequireAuthorization();

        group.MapPut("/{poolId:guid}", (Guid poolId, ClaimsPrincipal principal, UpdatePoolRequest request, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(pools.UpdatePool(poolId, userId, request));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/{poolId:guid}/invites", (Guid poolId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var invite = pools.CreateInvite(poolId, userId);
                return Results.Created($"/api/pools/invites/{invite.Code}", invite);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapGet("/invites/{code}", (string code, PoolStore pools) =>
        {
            var invite = pools.GetInvite(code);
            return invite is null ? Results.NotFound() : Results.Ok(invite);
        });

        group.MapPost("/join", (ClaimsPrincipal principal, JoinPoolRequest request, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(pools.JoinPool(userId, request));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/{poolId:guid}/markets", (Guid poolId, PoolStore pools) =>
        {
            return pools.GetPool(poolId) is null
                ? Results.NotFound()
                : Results.Ok(pools.GetMarkets(poolId));
        });

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
