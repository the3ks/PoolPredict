using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Modules.Markets;

public sealed record PayoutMarketRuleResponse(
    Guid Id,
    MarketProfile Profile,
    MarketType MarketType,
    MarketPeriod Period,
    decimal? LineValue,
    decimal PayoutMultiplier,
    bool IsEnabled);

public sealed record PayoutConfigurationResponse(
    Guid Id,
    int Version,
    string Name,
    bool IsActive,
    IReadOnlyCollection<PayoutMarketRuleResponse> Rules);
