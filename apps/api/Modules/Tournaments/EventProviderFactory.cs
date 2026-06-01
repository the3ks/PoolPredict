using Microsoft.Extensions.Options;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class EventProviderFactory(IServiceProvider services, IOptions<EventProviderOptions> options)
{
    public IEventProvider Create()
    {
        return options.Value.Provider.Trim().ToUpperInvariant() switch
        {
            "FOOTBALLDATA" or "FOOTBALL_DATA" or "FOOTBALL-DATA" => services.GetRequiredService<FootballDataProvider>(),
            "MOCK" => services.GetRequiredService<MockEventProvider>(),
            var provider => throw new InvalidOperationException($"Unsupported event provider '{provider}'.")
        };
    }
}
