using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class TournamentCatalog
{
    private readonly List<Tournament> _tournaments = [];
    private readonly List<Participant> _participants = [];
    private readonly List<Event> _events = [];
    private readonly Dictionary<string, Guid> _tournamentExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _participantExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _eventExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public TournamentCatalog(IEventProvider provider, ITournamentPersistence persistence)
    {
        LoadOrSync(provider, persistence).GetAwaiter().GetResult();
    }

    public IReadOnlyCollection<Tournament> GetTournaments()
    {
        lock (_gate)
        {
            return _tournaments.ToArray();
        }
    }

    public Tournament? GetTournament(Guid id)
    {
        lock (_gate)
        {
            return _tournaments.SingleOrDefault(tournament => tournament.Id == id);
        }
    }

    public IReadOnlyCollection<Participant> GetParticipants(Guid tournamentId)
    {
        lock (_gate)
        {
            return _participants.Where(participant => participant.TournamentId == tournamentId).ToArray();
        }
    }

    public IReadOnlyCollection<Event> GetEvents(Guid tournamentId)
    {
        lock (_gate)
        {
            return _events.Where(matchEvent => matchEvent.TournamentId == tournamentId).ToArray();
        }
    }

    private async Task LoadOrSync(IEventProvider provider, ITournamentPersistence persistence)
    {
        var persisted = await persistence.LoadAsync();
        if (persisted is not null)
        {
            lock (_gate)
            {
                _tournaments.AddRange(persisted.Tournaments);
                _participants.AddRange(persisted.Participants);
                _events.AddRange(persisted.Events);
            }

            return;
        }

        var snapshot = await SyncFromProvider(provider);
        await persistence.SaveAsync(snapshot);
    }

    private async Task<TournamentSyncSnapshot> SyncFromProvider(IEventProvider provider)
    {
        var tournaments = (await provider.GetTournamentsAsync()).ToArray();
        var syncedTournaments = new List<(Tournament Tournament, string ExternalId)>();
        var syncedParticipants = new List<(Participant Participant, string ExternalId)>();
        var syncedEvents = new List<(Event Event, string ExternalId)>();

        lock (_gate)
        {
            foreach (var tournamentDto in tournaments)
            {
                var tournamentId = Ids.NewId();
                var tournament = new Tournament(
                    tournamentId,
                    tournamentDto.Name,
                    tournamentDto.Sport,
                    tournamentDto.StartsOn,
                    tournamentDto.EndsOn);

                _tournamentExternalIds[tournamentDto.ExternalId] = tournamentId;
                _tournaments.Add(tournament);
                syncedTournaments.Add((tournament, tournamentDto.ExternalId));
            }
        }

        foreach (var tournamentDto in tournaments)
        {
            var tournamentId = _tournamentExternalIds[tournamentDto.ExternalId];
            var participants = await provider.GetParticipantsAsync(tournamentDto.ExternalId);
            var events = await provider.GetEventsAsync(tournamentDto.ExternalId);

            lock (_gate)
            {
                foreach (var participantDto in participants)
                {
                    var participantId = Ids.NewId();
                    var participant = new Participant(
                        participantId,
                        tournamentId,
                        participantDto.Name,
                        participantDto.Code,
                        participantDto.Country);

                    _participantExternalIds[participantDto.ExternalId] = participantId;
                    _participants.Add(participant);
                    syncedParticipants.Add((participant, participantDto.ExternalId));
                }

                foreach (var eventDto in events)
                {
                    var homeParticipantId = _participantExternalIds[eventDto.HomeParticipantExternalId];
                    var awayParticipantId = _participantExternalIds[eventDto.AwayParticipantExternalId];
                    var homeParticipant = _participants.Single(participant => participant.Id == homeParticipantId);
                    var awayParticipant = _participants.Single(participant => participant.Id == awayParticipantId);
                    var eventId = Ids.NewId();
                    var matchEvent = new Event(
                        eventId,
                        tournamentId,
                        homeParticipantId,
                        awayParticipantId,
                        homeParticipant.Name,
                        awayParticipant.Name,
                        eventDto.StartsAt,
                        eventDto.Status);

                    _eventExternalIds[eventDto.ExternalId] = eventId;
                    _events.Add(matchEvent);
                    syncedEvents.Add((matchEvent, eventDto.ExternalId));
                }
            }
        }

        return new TournamentSyncSnapshot(syncedTournaments, syncedParticipants, syncedEvents);
    }
}
