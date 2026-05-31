namespace PoolPredict.Api.Domain.Points;

public enum PointLedgerReason
{
    StartingBalance,
    PredictionSubmitted,
    SettlementPayout,
    SettlementRefund,
    AdminCorrection
}
