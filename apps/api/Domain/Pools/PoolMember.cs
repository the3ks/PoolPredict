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
        PoolMemberLeaderboardStatus leaderboardStatus = PoolMemberLeaderboardStatus.Ranked)
        : base(id)
    {
        PoolId = poolId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        LeaderboardStatus = leaderboardStatus;
    }

    public Guid PoolId { get; }

    public Guid UserId { get; }

    public PoolMemberRole Role { get; }

    public PoolMemberLeaderboardStatus LeaderboardStatus { get; private set; }

    public DateTimeOffset JoinedAt { get; }

    public void SetLeaderboardStatus(PoolMemberLeaderboardStatus leaderboardStatus)
    {
        LeaderboardStatus = leaderboardStatus;
    }
}
