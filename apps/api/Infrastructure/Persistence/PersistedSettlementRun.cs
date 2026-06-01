using PoolPredict.Api.Domain.Settlement;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedSettlementRun
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public SettlementRunStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<PersistedSettlementLog> Logs { get; set; } = [];
}
