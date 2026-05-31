using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class PoolMember : Entity
{
    public PoolMember(Guid id, Guid poolId, Guid userId, PoolMemberRole role, DateTimeOffset joinedAt)
        : base(id)
    {
        PoolId = poolId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid PoolId { get; }

    public Guid UserId { get; }

    public PoolMemberRole Role { get; }

    public DateTimeOffset JoinedAt { get; }
}
