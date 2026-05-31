using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Pools;

public sealed class Pool : Entity
{
    public Pool(Guid id, Guid ownerUserId, string name, Guid tournamentId, MarketProfile profile, int startingBalance)
        : base(id)
    {
        OwnerUserId = ownerUserId;
        Name = name;
        TournamentId = tournamentId;
        Profile = profile;
        StartingBalance = startingBalance;
    }

    public Guid OwnerUserId { get; }

    public string Name { get; private set; }

    public Guid TournamentId { get; }

    public MarketProfile Profile { get; }

    public int StartingBalance { get; private set; }

    public void UpdateSettings(string name, int startingBalance)
    {
        Name = name;
        StartingBalance = startingBalance;
    }
}
