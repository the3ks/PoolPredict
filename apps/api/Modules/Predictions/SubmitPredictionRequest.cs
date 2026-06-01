namespace PoolPredict.Api.Modules.Predictions;

public sealed record SubmitPredictionRequest(
    Guid PoolId,
    Guid MarketId,
    string SelectedOption,
    int Stake);
