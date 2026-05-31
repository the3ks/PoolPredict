using PoolPredict.Api.Domain.Points;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPointLedgerEntry
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid MemberId { get; set; }
    public int Points { get; set; }
    public PointLedgerReason Reason { get; set; }
    public Guid? PredictionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
