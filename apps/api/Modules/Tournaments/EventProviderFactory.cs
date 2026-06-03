using Microsoft.Extensions.Options;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class EventProviderFactory(IServiceProvider services, IOptions<EventProviderOptions> options)
{
    public string DefaultProvider => options.Value.Provider;

    public IReadOnlyCollection<string> GetAvailableProviders()
    {
        var providers = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Value.FootballData.BaseUrl))
        {
            providers.Add("FootballData");
        }

        if (!string.IsNullOrWhiteSpace(options.Value.VirtualProvider.BaseUrl))
        {
            providers.Add("VirtualProvider");
        }

        if (string.Equals(options.Value.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            providers.Add("Mock");
        }

        return providers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IEventProvider Create(string? providerName = null)
    {
        return Normalize(providerName ?? options.Value.Provider) switch
        {
            "VIRTUALPROVIDER" or "VIRTUAL_PROVIDER" or "VIRTUAL-PROVIDER" or "VIRTUAL" => services.GetRequiredService<VirtualProviderEventProvider>(),
            "FOOTBALLDATA" or "FOOTBALL_DATA" or "FOOTBALL-DATA" => services.GetRequiredService<FootballDataProvider>(),
            "MOCK" => services.GetRequiredService<MockEventProvider>(),
            var provider => throw new InvalidOperationException($"Unsupported event provider '{provider}'.")
        };
    }

    public static string CanonicalName(string providerName) =>
        Normalize(providerName) switch
        {
            "VIRTUALPROVIDER" or "VIRTUAL_PROVIDER" or "VIRTUAL-PROVIDER" or "VIRTUAL" => "VirtualProvider",
            "FOOTBALLDATA" or "FOOTBALL_DATA" or "FOOTBALL-DATA" => "FootballData",
            "MOCK" => "Mock",
            var provider => throw new InvalidOperationException($"Unsupported event provider '{provider}'.")
        };

    private static string Normalize(string providerName) =>
        providerName.Trim().ToUpperInvariant();
}
