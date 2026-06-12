namespace PoolPredict.Api.Domain.Points;

public enum PointLedgerReason
{
    StartingBalance,
    StartingBalanceAdjustment,
    PredictionSubmitted,
    PredictionCancelledRefund,
    SettlementPayout,
    SettlementRefund,
    AdminCorrection
}
