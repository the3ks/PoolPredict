namespace PoolPredict.Api.Modules.Tournaments;

public sealed class NoOpTournamentPersistence : ITournamentPersistence
{
    public Task<TournamentSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<TournamentSnapshot?>(null);

    public Task SaveAsync(TournamentSyncSnapshot snapshot, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
