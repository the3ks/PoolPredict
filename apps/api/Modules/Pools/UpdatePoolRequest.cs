namespace PoolPredict.Api.Modules.Pools;

public sealed class UpdatePoolRequest
{
    public string Name { get; init; } = "";
    public int StartingBalance { get; init; }
    public bool PredictionsLocked { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? AnnouncementTitle { get; init; }
    public int DefaultStake { get; init; }
    public int MinStake { get; init; }
    public int MaxStake { get; init; }
    public int MaxTotalStakePerEvent { get; init; }
    public bool VipEventStakeMultiplierEnabled { get; init; }
    public decimal LeaderboardMinEventAverageStakePercent { get; init; }
}
