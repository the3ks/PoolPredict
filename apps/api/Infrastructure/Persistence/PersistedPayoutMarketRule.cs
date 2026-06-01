using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPayoutMarketRule
{
    public Guid Id { get; set; }
    public Guid PayoutConfigurationId { get; set; }
    public MarketProfile Profile { get; set; }
    public MarketType MarketType { get; set; }
    public MarketPeriod Period { get; set; }
    public decimal? LineValue { get; set; }
    public decimal PayoutMultiplier { get; set; }
    public bool IsEnabled { get; set; }
    public PersistedPayoutConfiguration? PayoutConfiguration { get; set; }
}
