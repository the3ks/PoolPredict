using System.Security.Claims;
using PoolPredict.Api.Modules.Pools;

namespace PoolPredict.Api.Modules.Markets;

public static class MarketEndpoints
{
    public static IEndpointRouteBuilder MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/markets");

        group.MapGet("/payout-configurations", (ClaimsPrincipal principal, PayoutConfigurationStore configurations) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            return Results.Ok(configurations.GetConfigurations());
        }).RequireAuthorization();

        group.MapGet("/events/{eventId:guid}/handicap-lines", (Guid eventId, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            return Results.Ok(pools.GetHandicapLineMarkets(eventId));
        }).RequireAuthorization();

        group.MapPut("/events/{eventId:guid}/handicap-lines", (Guid eventId, ConfirmHandicapLineRequest request, ClaimsPrincipal principal, PoolStore pools) =>
        {
            if (!principal.IsInRole("PlatformAdmin"))
            {
                return Results.Forbid();
            }

            try
            {
                return Results.Ok(pools.ConfirmHandicapLine(eventId, request));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
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
}
