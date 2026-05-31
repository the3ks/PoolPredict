using PoolPredict.Api.Domain.Markets;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPrediction
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid MemberId { get; set; }
    public Guid MarketId { get; set; }
    public string SelectedOption { get; set; } = "";
    public int Stake { get; set; }
    public MarketType MarketType { get; set; }
    public MarketPeriod MarketPeriod { get; set; }
    public decimal? LineValueSnapshot { get; set; }
    public decimal PayoutMultiplierSnapshot { get; set; }
    public int PayoutConfigurationVersionSnapshot { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}
