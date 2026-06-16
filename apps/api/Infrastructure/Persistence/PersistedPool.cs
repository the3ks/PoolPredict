using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPool
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public Guid TournamentId { get; set; }
    public MarketProfile Profile { get; set; }
    public int StartingBalance { get; set; }
    public bool PredictionsLocked { get; set; }
    public string? CoverImageUrl { get; set; }
    public string AnnouncementTitle { get; set; } = "Announcements";
    public bool IsHidden { get; set; }
    public int DefaultStake { get; set; }
    public int MinStake { get; set; }
    public int MaxStake { get; set; }
    public int MaxTotalStakePerEvent { get; set; }
    public decimal LeaderboardMinEventAverageStakePercent { get; set; }
}
