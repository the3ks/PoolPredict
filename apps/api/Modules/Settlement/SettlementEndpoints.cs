using System.Security.Claims;

namespace PoolPredict.Api.Modules.Settlement;

public static class SettlementEndpoints
{
    public static IEndpointRouteBuilder MapSettlementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settlement").RequireAuthorization();

        group.MapPost("/events/{eventId:guid}/result", async (
            Guid eventId,
            ClaimsPrincipal principal,
            SetEventResultRequest request,
            SettlementService settlement,
            CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(await settlement.RecordResultAndSettleAsync(eventId, request, cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
