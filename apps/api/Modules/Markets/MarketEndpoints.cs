using System.Security.Claims;

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

        return app;
    }
}
