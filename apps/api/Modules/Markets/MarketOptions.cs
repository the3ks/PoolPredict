namespace PoolPredict.Api.Modules.Markets;

public sealed class MarketOptions
{
    public int HandicapOpenWindowHours { get; set; } = 24;
    public int ScheduledDisplayWindowHours { get; set; } = 48;
}
