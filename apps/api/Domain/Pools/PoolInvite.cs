using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class PoolInvite : Entity
{
    public PoolInvite(Guid id, Guid poolId, Guid createdByUserId, string code, DateTimeOffset createdAt)
        : base(id)
    {
        PoolId = poolId;
        CreatedByUserId = createdByUserId;
        Code = code;
        CreatedAt = createdAt;
    }

    public Guid PoolId { get; }

    public Guid CreatedByUserId { get; }

    public string Code { get; }

    public DateTimeOffset CreatedAt { get; }
}
