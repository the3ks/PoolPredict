namespace PoolPredict.Api.Modules.Tournaments;

public interface ITournamentPersistence
{
    Task<TournamentSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(TournamentSyncSnapshot snapshot, CancellationToken cancellationToken = default);
}
