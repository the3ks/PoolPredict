using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;

namespace PoolPredict.Api.Domain.Predictions;

public sealed class Prediction : Entity
{
    public Prediction(
        Guid id,
        Guid poolId,
        Guid memberId,
        Guid marketId,
        string selectedOption,
        int stake,
        MarketType marketType,
        MarketPeriod marketPeriod,
        decimal? lineValueSnapshot,
        decimal payoutMultiplierSnapshot,
        int payoutConfigurationVersionSnapshot,
        PredictionStatus status = PredictionStatus.Active)
        : base(id)
    {
        PoolId = poolId;
        MemberId = memberId;
        MarketId = marketId;
        SelectedOption = selectedOption;
        Stake = stake;
        MarketType = marketType;
        MarketPeriod = marketPeriod;
        LineValueSnapshot = lineValueSnapshot;
        PayoutMultiplierSnapshot = payoutMultiplierSnapshot;
        PayoutConfigurationVersionSnapshot = payoutConfigurationVersionSnapshot;
        Status = status;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    public Guid PoolId { get; }

    public Guid MemberId { get; }

    public Guid MarketId { get; }

    public string SelectedOption { get; }

    public int Stake { get; }

    public MarketType MarketType { get; }

    public MarketPeriod MarketPeriod { get; }

    public decimal? LineValueSnapshot { get; }

    public decimal PayoutMultiplierSnapshot { get; }

    public int PayoutConfigurationVersionSnapshot { get; }

    public PredictionStatus Status { get; private set; }

    public DateTimeOffset SubmittedAt { get; }

    public void Cancel()
    {
        if (Status == PredictionStatus.Cancelled)
        {
            return;
        }

        Status = PredictionStatus.Cancelled;
    }
}
