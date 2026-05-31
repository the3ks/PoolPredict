using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Markets;

public sealed class Market : Entity
{
    public Market(
        Guid id,
        Guid poolId,
        Guid eventId,
        MarketType type,
        MarketPeriod period,
        decimal? lineValue,
        decimal payoutMultiplier,
        int payoutConfigurationVersion)
        : base(id)
    {
        PoolId = poolId;
        EventId = eventId;
        Type = type;
        Period = period;
        LineValue = lineValue;
        PayoutMultiplier = payoutMultiplier;
        PayoutConfigurationVersion = payoutConfigurationVersion;
    }

    public Guid PoolId { get; }

    public Guid EventId { get; }

    public MarketType Type { get; }

    public MarketPeriod Period { get; }

    public decimal? LineValue { get; private set; }

    public decimal PayoutMultiplier { get; }

    public int PayoutConfigurationVersion { get; }

    public MarketStatus Status { get; private set; } = MarketStatus.Open;

    public void UpdateLineValue(decimal? lineValue)
    {
        if (Status is MarketStatus.Locked or MarketStatus.Settled or MarketStatus.Voided)
        {
            throw new InvalidOperationException("Market line values cannot be changed after lock.");
        }

        LineValue = lineValue;
    }
}
