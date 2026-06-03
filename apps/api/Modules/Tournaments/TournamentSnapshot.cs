using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed record TournamentSnapshot(
    IReadOnlyCollection<Tournament> Tournaments,
    IReadOnlyCollection<Participant> Participants,
    IReadOnlyCollection<Event> Events,
    IReadOnlyDictionary<Guid, ProviderSourceInfo> TournamentSources,
    IReadOnlyDictionary<Guid, ProviderSourceInfo> ParticipantSources,
    IReadOnlyDictionary<Guid, ProviderSourceInfo> EventSources,
    IReadOnlyDictionary<Guid, EventManagementMode> EventManagementModes,
    IReadOnlyDictionary<string, Guid> TournamentExternalIds,
    IReadOnlyDictionary<string, Guid> ParticipantExternalIds,
    IReadOnlyDictionary<string, Guid> EventExternalIds);

public sealed record TournamentSyncSnapshot(
    string Provider,
    bool IsTestData,
    IReadOnlyCollection<(Tournament Tournament, string ExternalId)> Tournaments,
    IReadOnlyCollection<(Participant Participant, string TournamentExternalId, string ExternalId)> Participants,
    IReadOnlyCollection<(Event Event, string TournamentExternalId, string ExternalId)> Events,
    IReadOnlyCollection<ProviderEventResult> EventResults);

public sealed record ProviderEventResult(
    Guid EventId,
    int FullTimeHomeScore,
    int FullTimeAwayScore,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore);

public sealed record ProviderSourceInfo(string Provider, bool IsTestData);

public sealed record TournamentResponse(
    Guid Id,
    string Name,
    string Sport,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string Provider,
    bool IsTestData);

public sealed record EventResponse(
    Guid Id,
    Guid TournamentId,
    Guid HomeParticipantId,
    Guid AwayParticipantId,
    string HomeParticipant,
    string AwayParticipant,
    DateTimeOffset StartsAt,
    EventStatus Status,
    string Provider,
    bool IsTestData,
    EventManagementMode ManagementMode);

public sealed record AdminEventResponse(
    Guid Id,
    Guid TournamentId,
    string TournamentName,
    string HomeParticipant,
    string AwayParticipant,
    DateTimeOffset StartsAt,
    EventStatus Status,
    string Provider,
    bool IsTestData,
    EventManagementMode ManagementMode,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore,
    int? FullTimeHomeScore,
    int? FullTimeAwayScore,
    DateTimeOffset? ResultRecordedAt);

public sealed record SetEventManagementModeRequest(EventManagementMode ManagementMode);

public sealed record UpdateManualEventRequest(
    DateTimeOffset StartsAt,
    EventStatus Status,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore,
    int? FullTimeHomeScore,
    int? FullTimeAwayScore);
