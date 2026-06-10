using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Modules.Tournaments;
using System.Security.Claims;

namespace PoolPredict.Api.Modules.Predictions;

public static class PredictionEndpoints
{
    public static IEndpointRouteBuilder MapPredictionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/predictions");

        group.MapPost("/", (ClaimsPrincipal principal, SubmitPredictionRequest request, PredictionStore predictions, PoolStore pools, TournamentCatalog catalog) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var prediction = predictions.Submit(request, userId, pools, catalog);
                return Results.Created($"/api/predictions/{prediction.Id}", prediction);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/pool/{poolId:guid}", (Guid poolId, ClaimsPrincipal principal, PredictionStore predictions, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var member = pools.GetMember(poolId, userId);
            return member is null
                ? Results.Forbid()
                : Results.Ok(predictions.GetMemberPredictionHistory(poolId, member.Id));
        }).RequireAuthorization();

        group.MapGet("/balance", (Guid poolId, ClaimsPrincipal principal, PredictionStore predictions, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var pool = pools.GetPoolForUser(poolId, userId);
            return pool is null
                ? Results.Forbid()
                : Results.Ok(new { poolId, memberId = pool.MemberId, balance = predictions.GetBalance(pool) });
        }).RequireAuthorization();

        group.MapGet("/pool/{poolId:guid}/leaderboard", (Guid poolId, ClaimsPrincipal principal, PredictionStore predictions, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var pool = pools.GetPoolForUser(poolId, userId);
            return pool is null
                ? Results.Forbid()
                : Results.Ok(predictions.GetLeaderboard(poolId, pool.StartingBalance));
        }).RequireAuthorization();

        group.MapGet("/pool/{poolId:guid}/market-summaries", (Guid poolId, ClaimsPrincipal principal, PredictionStore predictions, PoolStore pools) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            return pools.GetMember(poolId, userId) is null
                ? Results.Forbid()
                : Results.Ok(predictions.GetMarketPredictionSummaries(poolId));
        }).RequireAuthorization();

        group.MapPost("/pool/{poolId:guid}/auto-pick/preview", (Guid poolId, ClaimsPrincipal principal, AutoPickPredictionsRequest request, PredictionStore predictions, PoolStore pools, TournamentCatalog catalog) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(predictions.PreviewAutoPick(poolId, request, userId, pools, catalog));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/pool/{poolId:guid}/auto-pick", (Guid poolId, ClaimsPrincipal principal, AutoPickPredictionsRequest request, PredictionStore predictions, PoolStore pools, TournamentCatalog catalog) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(predictions.SubmitAutoPick(poolId, request, userId, pools, catalog));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization();

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
