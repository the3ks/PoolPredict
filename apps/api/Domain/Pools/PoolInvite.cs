using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class PoolInvite : Entity
{
    public PoolInvite(Guid id, Guid poolId, Guid createdByUserId, string code, DateTimeOffset createdAt, DateTimeOffset? revokedAt = null, Guid? revokedByUserId = null)
        : base(id)
    {
        PoolId = poolId;
        CreatedByUserId = createdByUserId;
        Code = code;
        CreatedAt = createdAt;
        RevokedAt = revokedAt;
        RevokedByUserId = revokedByUserId;
    }

    public Guid PoolId { get; }

    public Guid CreatedByUserId { get; }

    public string Code { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public void Revoke(Guid revokedByUserId, DateTimeOffset revokedAt)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedByUserId = revokedByUserId;
        RevokedAt = revokedAt;
    }
}
