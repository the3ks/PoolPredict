namespace PoolPredict.Api.Modules.Settlement;

public sealed record SetEventResultRequest(
    int FullTimeHomeScore,
    int FullTimeAwayScore,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore,
    bool IsCancelled = false);
