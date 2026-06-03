namespace PoolPredict.Api.Modules.Tournaments;

public sealed class TournamentSyncJob(TournamentCatalog catalog, ILogger<TournamentSyncJob> logger)
{
    public async Task<ProviderSyncStatus> ExecuteAsync(string? providerName = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting tournament provider sync. Provider={Provider}", providerName ?? "default");
        var status = await catalog.SyncFromProviderAsync(providerName, cancellationToken);
        logger.LogInformation(
            "Tournament provider sync completed. Provider={Provider} Tournaments={TournamentCount} Participants={ParticipantCount} Events={EventCount}",
            status.Provider,
            status.TournamentCount,
            status.ParticipantCount,
            status.EventCount);

        return status;
    }
}
