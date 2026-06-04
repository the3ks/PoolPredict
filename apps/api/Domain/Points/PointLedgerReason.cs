namespace PoolPredict.Api.Domain.Points;

public enum PointLedgerReason
{
    StartingBalance,
    StartingBalanceAdjustment,
    PredictionSubmitted,
    SettlementPayout,
    SettlementRefund,
    AdminCorrection
}
