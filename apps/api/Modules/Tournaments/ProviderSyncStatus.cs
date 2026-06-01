namespace PoolPredict.Api.Modules.Tournaments;

public sealed record ProviderSyncStatus(
    string Provider,
    DateTimeOffset? LastSyncedAt,
    string LastResult,
    int TournamentCount,
    int ParticipantCount,
    int EventCount);
