namespace PoolPredict.Api.Modules.Pools;

public sealed record UpdatePoolRequest(
    string Name,
    int StartingBalance,
    bool PredictionsLocked,
    string? CoverImageUrl,
    int DefaultStake,
    int MinStake,
    int MaxStake,
    int MaxTotalStakePerEvent);
