using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class TournamentCatalog
{
    private readonly IEventProvider _provider;
    private readonly ITournamentPersistence _persistence;
    private readonly string _providerName;
    private readonly List<Tournament> _tournaments = [];
    private readonly List<Participant> _participants = [];
    private readonly List<Event> _events = [];
    private readonly Dictionary<string, Guid> _tournamentExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _participantExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _eventExternalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private DateTimeOffset? _lastSyncedAt;
    private string _lastResult = "Not synced.";

    public TournamentCatalog(EventProviderFactory providerFactory, ITournamentPersistence persistence, IConfiguration configuration)
    {
        _provider = providerFactory.Create();
        _persistence = persistence;
        _providerName = configuration["EventProvider:Provider"] ?? "Mock";
        LoadOrSync(IsMockProvider()).GetAwaiter().GetResult();
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

    public Event? GetEvent(Guid eventId)
    {
        lock (_gate)
        {
            return _events.SingleOrDefault(matchEvent => matchEvent.Id == eventId);
        }
    }

    public ProviderSyncStatus GetProviderStatus()
    {
        lock (_gate)
        {
            return new ProviderSyncStatus(
                _providerName,
                _lastSyncedAt,
                _lastResult,
                _tournaments.Count,
                _participants.Count,
                _events.Count);
        }
    }

    public async Task<ProviderSyncStatus> SyncFromProviderAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await SyncFromProvider(cancellationToken);
            await _persistence.SaveAsync(snapshot, cancellationToken);
            _lastSyncedAt = DateTimeOffset.UtcNow;
            _lastResult = "Provider sync completed.";
        }
        catch (Exception ex)
        {
            _lastResult = ex.Message;
            throw;
        }

        return GetProviderStatus();
    }

    private async Task LoadOrSync(bool autoSyncWhenEmpty)
    {
        var persisted = await _persistence.LoadAsync();
        if (persisted is not null)
        {
            lock (_gate)
            {
                _tournaments.AddRange(persisted.Tournaments);
                _participants.AddRange(persisted.Participants);
                _events.AddRange(persisted.Events);
                foreach (var item in persisted.TournamentExternalIds)
                {
                    _tournamentExternalIds[item.Key] = item.Value;
                }

                foreach (var item in persisted.ParticipantExternalIds)
                {
                    _participantExternalIds[item.Key] = item.Value;
                }

                foreach (var item in persisted.EventExternalIds)
                {
                    _eventExternalIds[item.Key] = item.Value;
                }

                _lastResult = "Loaded from persistence.";
            }

            return;
        }

        if (autoSyncWhenEmpty)
        {
            await SyncFromProviderAsync();
            return;
        }

        _lastResult = "No persisted tournament data. Run provider sync from Admin.";
    }

    private bool IsMockProvider() =>
        string.Equals(_providerName, "Mock", StringComparison.OrdinalIgnoreCase);

    private async Task<TournamentSyncSnapshot> SyncFromProvider(CancellationToken cancellationToken = default)
    {
        var tournaments = (await _provider.GetTournamentsAsync(cancellationToken)).ToArray();
        var syncedTournaments = new List<(Tournament Tournament, string ExternalId)>();
        var syncedParticipants = new List<(Participant Participant, string ExternalId)>();
        var syncedEvents = new List<(Event Event, string ExternalId)>();
        var existingTournamentIds = new Dictionary<string, Guid>(_tournamentExternalIds, StringComparer.OrdinalIgnoreCase);
        var existingParticipantIds = new Dictionary<string, Guid>(_participantExternalIds, StringComparer.OrdinalIgnoreCase);
        var existingEventIds = new Dictionary<string, Guid>(_eventExternalIds, StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            _tournamentExternalIds.Clear();
            _participantExternalIds.Clear();
            _eventExternalIds.Clear();
            _tournaments.Clear();
            _participants.Clear();
            _events.Clear();

            foreach (var tournamentDto in tournaments)
            {
                var tournamentId = existingTournamentIds.GetValueOrDefault(tournamentDto.ExternalId, Ids.NewId());
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
            var participants = await _provider.GetParticipantsAsync(tournamentDto.ExternalId, cancellationToken);
            var events = await _provider.GetEventsAsync(tournamentDto.ExternalId, cancellationToken);

            lock (_gate)
            {
                foreach (var participantDto in participants)
                {
                    var participantId = existingParticipantIds.GetValueOrDefault(participantDto.ExternalId, Ids.NewId());
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
                    var eventId = existingEventIds.GetValueOrDefault(eventDto.ExternalId, Ids.NewId());
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
