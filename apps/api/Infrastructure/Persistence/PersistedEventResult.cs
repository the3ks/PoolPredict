namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedEventResult
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public int FullTimeHomeScore { get; set; }
    public int FullTimeAwayScore { get; set; }
    public int? FirstHalfHomeScore { get; set; }
    public int? FirstHalfAwayScore { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
