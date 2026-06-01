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
                : Results.Ok(predictions.GetMemberPredictions(poolId, member.Id));
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

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
