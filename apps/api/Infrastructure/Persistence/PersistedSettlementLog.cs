using PoolPredict.Api.Domain.Settlement;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedSettlementLog
{
    public Guid Id { get; set; }
    public Guid SettlementRunId { get; set; }
    public Guid? PredictionId { get; set; }
    public SettlementLogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public PersistedSettlementRun? SettlementRun { get; set; }
}
