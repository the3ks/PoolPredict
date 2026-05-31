using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Points;

public sealed class PointLedgerEntry : Entity
{
    public PointLedgerEntry(Guid id, Guid poolId, Guid memberId, int points, PointLedgerReason reason, Guid? predictionId)
        : base(id)
    {
        PoolId = poolId;
        MemberId = memberId;
        Points = points;
        Reason = reason;
        PredictionId = predictionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid PoolId { get; }

    public Guid MemberId { get; }

    public int Points { get; }

    public PointLedgerReason Reason { get; }

    public Guid? PredictionId { get; }

    public DateTimeOffset CreatedAt { get; }
}
