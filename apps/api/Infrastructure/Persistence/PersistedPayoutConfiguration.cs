namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPayoutConfiguration
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<PersistedPayoutMarketRule> Rules { get; set; } = [];
}
