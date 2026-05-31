using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedPool
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public Guid TournamentId { get; set; }
    public MarketProfile Profile { get; set; }
    public int StartingBalance { get; set; }
}
