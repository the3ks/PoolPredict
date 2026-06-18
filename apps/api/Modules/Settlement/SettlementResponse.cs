namespace PoolPredict.Api.Modules.Settlement;

public sealed record SettlementResponse(
    Guid SettlementRunId,
    Guid EventId,
    int SettledPredictions,
    int UnchangedPredictions,
    int LedgerEntriesCreated,
    string Status,
    bool BackupAttempted,
    bool BackupSucceeded,
    string? BackupMessage);
