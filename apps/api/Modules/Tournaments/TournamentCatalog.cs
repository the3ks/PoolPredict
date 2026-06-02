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
    private readonly Dictionary<Guid, ProviderSourceInfo> _tournamentSources = [];
    private readonly Dictionary<Guid, ProviderSourceInfo> _participantSources = [];
    private readonly Dictionary<Guid, ProviderSourceInfo> _eventSources = [];
    private readonly Dictionary<Guid, EventManagementMode> _eventManagementModes = [];
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

    public IReadOnlyCollection<TournamentResponse> GetTournamentResponses()
    {
        lock (_gate)
        {
            return _tournaments.Select(tournament =>
            {
                var source = _tournamentSources.GetValueOrDefault(tournament.Id, UnknownSource);
                return new TournamentResponse(
                    tournament.Id,
                    tournament.Name,
                    tournament.Sport,
                    tournament.StartsOn,
                    tournament.EndsOn,
                    source.Provider,
                    source.IsTestData);
            }).ToArray();
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

    public IReadOnlyCollection<EventResponse> GetEventResponses(Guid tournamentId)
    {
        lock (_gate)
        {
            return _events.Where(matchEvent => matchEvent.TournamentId == tournamentId)
                .Select(matchEvent =>
                {
                    var source = _eventSources.GetValueOrDefault(matchEvent.Id, UnknownSource);
                    return new EventResponse(
                        matchEvent.Id,
                        matchEvent.TournamentId,
                        matchEvent.HomeParticipantId,
                        matchEvent.AwayParticipantId,
                        matchEvent.HomeParticipant,
                        matchEvent.AwayParticipant,
                        matchEvent.StartsAt,
                        matchEvent.Status,
                        source.Provider,
                        source.IsTestData,
                        _eventManagementModes.GetValueOrDefault(matchEvent.Id, EventManagementMode.Provider));
                })
                .ToArray();
        }
    }

    public Event? GetEvent(Guid eventId)
    {
        lock (_gate)
        {
            return _events.SingleOrDefault(matchEvent => matchEvent.Id == eventId);
        }
    }

    public void SetEventState(Guid eventId, DateTimeOffset startsAt, EventStatus status, EventManagementMode managementMode)
    {
        lock (_gate)
        {
            var existing = _events.SingleOrDefault(matchEvent => matchEvent.Id == eventId);
            if (existing is null)
            {
                return;
            }

            _events.RemoveAll(matchEvent => matchEvent.Id == eventId);
            _events.Add(new Event(
                existing.Id,
                existing.TournamentId,
                existing.HomeParticipantId,
                existing.AwayParticipantId,
                existing.HomeParticipant,
                existing.AwayParticipant,
                startsAt,
                status));
            _eventManagementModes[eventId] = managementMode;
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
                foreach (var item in persisted.TournamentSources)
                {
                    _tournamentSources[item.Key] = item.Value;
                }

                foreach (var item in persisted.ParticipantSources)
                {
                    _participantSources[item.Key] = item.Value;
                }

                foreach (var item in persisted.EventSources)
                {
                    _eventSources[item.Key] = item.Value;
                }

                foreach (var item in persisted.EventManagementModes)
                {
                    _eventManagementModes[item.Key] = item.Value;
                }

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
        var syncedParticipants = new List<(Participant Participant, string TournamentExternalId, string ExternalId)>();
        var syncedEvents = new List<(Event Event, string TournamentExternalId, string ExternalId)>();
        var existingTournamentIds = new Dictionary<string, Guid>(_tournamentExternalIds, StringComparer.OrdinalIgnoreCase);
        var existingParticipantIds = new Dictionary<string, Guid>(_participantExternalIds, StringComparer.OrdinalIgnoreCase);
        var existingEventIds = new Dictionary<string, Guid>(_eventExternalIds, StringComparer.OrdinalIgnoreCase);
        var source = new ProviderSourceInfo(_providerName, IsMockProvider());

        lock (_gate)
        {
            foreach (var tournamentDto in tournaments)
            {
                var key = TournamentKey(_providerName, tournamentDto.ExternalId);
                var tournamentId = existingTournamentIds.GetValueOrDefault(key, Ids.NewId());
                var tournament = new Tournament(
                    tournamentId,
                    tournamentDto.Name,
                    tournamentDto.Sport,
                    tournamentDto.StartsOn,
                    tournamentDto.EndsOn);

                _tournaments.RemoveAll(item => item.Id == tournamentId);
                _tournamentExternalIds[key] = tournamentId;
                _tournamentSources[tournamentId] = source;
                _tournaments.Add(tournament);
                syncedTournaments.Add((tournament, tournamentDto.ExternalId));
            }
        }

        foreach (var tournamentDto in tournaments)
        {
            var tournamentId = _tournamentExternalIds[TournamentKey(_providerName, tournamentDto.ExternalId)];
            var participants = await _provider.GetParticipantsAsync(tournamentDto.ExternalId, cancellationToken);
            var events = await _provider.GetEventsAsync(tournamentDto.ExternalId, cancellationToken);

            lock (_gate)
            {
                foreach (var participantDto in participants)
                {
                    var key = ParticipantKey(_providerName, tournamentId, participantDto.ExternalId);
                    var participantId = existingParticipantIds.GetValueOrDefault(key, Ids.NewId());
                    var participant = new Participant(
                        participantId,
                        tournamentId,
                        participantDto.Name,
                        participantDto.Code,
                        participantDto.Country);

                    _participants.RemoveAll(item => item.Id == participantId);
                    _participantExternalIds[key] = participantId;
                    _participantSources[participantId] = source;
                    _participants.Add(participant);
                    syncedParticipants.Add((participant, tournamentDto.ExternalId, participantDto.ExternalId));
                }

                foreach (var eventDto in events)
                {
                    var homeParticipantId = _participantExternalIds[ParticipantKey(_providerName, tournamentId, eventDto.HomeParticipantExternalId)];
                    var awayParticipantId = _participantExternalIds[ParticipantKey(_providerName, tournamentId, eventDto.AwayParticipantExternalId)];
                    var homeParticipant = _participants.Single(participant => participant.Id == homeParticipantId);
                    var awayParticipant = _participants.Single(participant => participant.Id == awayParticipantId);
                    var key = EventKey(_providerName, tournamentId, eventDto.ExternalId);
                    var eventId = existingEventIds.GetValueOrDefault(key, Ids.NewId());
                    if (_eventManagementModes.GetValueOrDefault(eventId, EventManagementMode.Provider) == EventManagementMode.Manual)
                    {
                        continue;
                    }

                    var matchEvent = new Event(
                        eventId,
                        tournamentId,
                        homeParticipantId,
                        awayParticipantId,
                        homeParticipant.Name,
                        awayParticipant.Name,
                        eventDto.StartsAt,
                        eventDto.Status);

                    _events.RemoveAll(item => item.Id == eventId);
                    _eventExternalIds[key] = eventId;
                    _eventSources[eventId] = source;
                    _eventManagementModes[eventId] = EventManagementMode.Provider;
                    _events.Add(matchEvent);
                    syncedEvents.Add((matchEvent, tournamentDto.ExternalId, eventDto.ExternalId));
                }
            }
        }

        return new TournamentSyncSnapshot(_providerName, IsMockProvider(), syncedTournaments, syncedParticipants, syncedEvents);
    }

    private static readonly ProviderSourceInfo UnknownSource = new("Unknown", false);

    private static string TournamentKey(string provider, string externalId) =>
        $"{provider}::{externalId}";

    private static string ParticipantKey(string provider, Guid tournamentId, string externalId) =>
        $"{provider}::{tournamentId:N}::{externalId}";

    private static string EventKey(string provider, Guid tournamentId, string externalId) =>
        $"{provider}::{tournamentId:N}::{externalId}";
}
