namespace PoolPredict.Api.Modules.Tournaments;

public interface IEventProvider
{
    Task SyncTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    Task<IEnumerable<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<ParticipantDto>> GetParticipantsAsync(string tournamentExternalId, CancellationToken cancellationToken = default);

    Task<IEnumerable<EventDto>> GetEventsAsync(string tournamentExternalId, CancellationToken cancellationToken = default);

    Task<IEnumerable<EventDto>> GetLiveEventsAsync(CancellationToken cancellationToken = default);

    Task<EventDto?> GetEventAsync(string externalId, CancellationToken cancellationToken = default);
}
