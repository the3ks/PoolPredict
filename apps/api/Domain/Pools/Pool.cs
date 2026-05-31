using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class Pool : Entity
{
    public Pool(Guid id, string name, Guid tournamentId, MarketProfile profile, int startingBalance)
        : base(id)
    {
        Name = name;
        TournamentId = tournamentId;
        Profile = profile;
        StartingBalance = startingBalance;
    }

    public string Name { get; }

    public Guid TournamentId { get; }

    public MarketProfile Profile { get; }

    public int StartingBalance { get; }
}
