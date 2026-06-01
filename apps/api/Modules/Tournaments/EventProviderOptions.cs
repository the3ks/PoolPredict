namespace PoolPredict.Api.Modules.Tournaments;

public sealed class EventProviderOptions
{
    public string Provider { get; set; } = "Mock";

    public FootballDataOptions FootballData { get; set; } = new();
}

public sealed class FootballDataOptions
{
    public string BaseUrl { get; set; } = "https://api.football-data.org/v4";

    public string ApiToken { get; set; } = "";

    public string CompetitionCode { get; set; } = "WC";

    public int Season { get; set; } = 2026;

    public string TournamentName { get; set; } = "FIFA World Cup 2026";

    public string Sport { get; set; } = "Football";

    public DateOnly StartsOn { get; set; } = new(2026, 6, 11);

    public DateOnly EndsOn { get; set; } = new(2026, 7, 19);
}
