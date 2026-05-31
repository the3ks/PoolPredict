using PoolPredict.Api.Domain.Pools;

namespace PoolPredict.Api.Modules.Pools;

public sealed record CreatePoolRequest(
    string Name,
    Guid TournamentId,
    MarketProfile Profile,
    int StartingBalance);
