using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed record TournamentSnapshot(
    IReadOnlyCollection<Tournament> Tournaments,
    IReadOnlyCollection<Participant> Participants,
    IReadOnlyCollection<Event> Events);

public sealed record TournamentSyncSnapshot(
    IReadOnlyCollection<(Tournament Tournament, string ExternalId)> Tournaments,
    IReadOnlyCollection<(Participant Participant, string ExternalId)> Participants,
    IReadOnlyCollection<(Event Event, string ExternalId)> Events);
