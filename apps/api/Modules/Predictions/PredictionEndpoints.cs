using PoolPredict.Api.Modules.Pools;

namespace PoolPredict.Api.Modules.Predictions;

public static class PredictionEndpoints
{
    public static IEndpointRouteBuilder MapPredictionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/predictions");

        group.MapPost("/", (SubmitPredictionRequest request, PredictionStore predictions, PoolStore pools) =>
        {
            try
            {
                var prediction = predictions.Submit(request, pools);
                return Results.Created($"/api/predictions/{prediction.Id}", prediction);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapGet("/pool/{poolId:guid}", (Guid poolId, PredictionStore predictions) =>
            Results.Ok(predictions.GetPoolPredictions(poolId)));

        group.MapGet("/balance", (Guid poolId, Guid memberId, PredictionStore predictions) =>
            Results.Ok(new { poolId, memberId, balance = predictions.GetBalance(poolId, memberId) }));

        return app;
    }
}
