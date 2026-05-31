using PoolPredict.Api.Domain.Markets;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedMarket
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid EventId { get; set; }
    public MarketType Type { get; set; }
    public MarketPeriod Period { get; set; }
    public decimal? LineValue { get; set; }
    public decimal PayoutMultiplier { get; set; }
    public int PayoutConfigurationVersion { get; set; }
    public MarketStatus Status { get; set; }
}
