using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class PoolMember : Entity
{
    public PoolMember(
        Guid id,
        Guid poolId,
        Guid userId,
        PoolMemberRole role,
        DateTimeOffset joinedAt,
        PoolMemberLeaderboardStatus leaderboardStatus = PoolMemberLeaderboardStatus.Ranked,
        int vipAdjustmentAmount = 0)
        : base(id)
    {
        PoolId = poolId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        LeaderboardStatus = leaderboardStatus;
        VipAdjustmentAmount = vipAdjustmentAmount;
    }

    public Guid PoolId { get; }

    public Guid UserId { get; }

    public PoolMemberRole Role { get; }

    public PoolMemberLeaderboardStatus LeaderboardStatus { get; private set; }

    public int VipAdjustmentAmount { get; private set; }

    public bool IsVip => VipAdjustmentAmount > 0;

    public DateTimeOffset JoinedAt { get; }

    public void SetLeaderboardStatus(PoolMemberLeaderboardStatus leaderboardStatus)
    {
        LeaderboardStatus = leaderboardStatus;
    }

    public void AddVipAdjustment(int amount)
    {
        if (amount == 0)
        {
            throw new ArgumentException("VIP adjustment amount cannot be zero.", nameof(amount));
        }

        VipAdjustmentAmount += amount;
    }
}
