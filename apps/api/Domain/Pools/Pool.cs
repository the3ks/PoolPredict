using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class Pool : Entity
{
    public Pool(
        Guid id,
        Guid ownerUserId,
        string name,
        Guid tournamentId,
        MarketProfile profile,
        int startingBalance,
        bool predictionsLocked = false,
        string? coverImageUrl = null,
        int defaultStake = 100,
        int minStake = 10,
        int maxStake = 200,
        int maxTotalStakePerEvent = 400,
        bool vipEventStakeMultiplierEnabled = false,
        decimal leaderboardMinEventAverageStakePercent = 0m,
        string announcementTitle = "Announcements",
        bool isHidden = false)
        : base(id)
    {
        OwnerUserId = ownerUserId;
        Name = name;
        TournamentId = tournamentId;
        Profile = profile;
        StartingBalance = startingBalance;
        PredictionsLocked = predictionsLocked;
        CoverImageUrl = coverImageUrl;
        DefaultStake = defaultStake;
        MinStake = minStake;
        MaxStake = maxStake;
        MaxTotalStakePerEvent = maxTotalStakePerEvent;
        VipEventStakeMultiplierEnabled = vipEventStakeMultiplierEnabled;
        LeaderboardMinEventAverageStakePercent = leaderboardMinEventAverageStakePercent;
        AnnouncementTitle = announcementTitle;
        IsHidden = isHidden;
    }

    public Guid OwnerUserId { get; }

    public string Name { get; private set; }

    public Guid TournamentId { get; }

    public MarketProfile Profile { get; }

    public int StartingBalance { get; private set; }

    public bool PredictionsLocked { get; private set; }

    public string? CoverImageUrl { get; private set; }

    public int DefaultStake { get; private set; }

    public int MinStake { get; private set; }

    public int MaxStake { get; private set; }

    public int MaxTotalStakePerEvent { get; private set; }

    public bool VipEventStakeMultiplierEnabled { get; private set; }

    public decimal LeaderboardMinEventAverageStakePercent { get; private set; }

    public string AnnouncementTitle { get; private set; }

    public bool IsHidden { get; private set; }

    public void UpdateSettings(
        string name,
        int startingBalance,
        bool predictionsLocked,
        string? coverImageUrl,
        int defaultStake,
        int minStake,
        int maxStake,
        int maxTotalStakePerEvent,
        bool vipEventStakeMultiplierEnabled,
        decimal leaderboardMinEventAverageStakePercent,
        string announcementTitle)
    {
        Name = name;
        StartingBalance = startingBalance;
        PredictionsLocked = predictionsLocked;
        CoverImageUrl = coverImageUrl;
        DefaultStake = defaultStake;
        MinStake = minStake;
        MaxStake = maxStake;
        MaxTotalStakePerEvent = maxTotalStakePerEvent;
        VipEventStakeMultiplierEnabled = vipEventStakeMultiplierEnabled;
        LeaderboardMinEventAverageStakePercent = leaderboardMinEventAverageStakePercent;
        AnnouncementTitle = announcementTitle;
    }

    public void SetHidden(bool isHidden)
    {
        IsHidden = isHidden;
    }
}
