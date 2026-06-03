using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed record TournamentDto(
    string ExternalId,
    string Name,
    string Sport,
    DateOnly StartsOn,
    DateOnly EndsOn);

public sealed record ParticipantDto(
    string ExternalId,
    string TournamentExternalId,
    string Name,
    string Code,
    string Country);

public sealed record EventDto(
    string ExternalId,
    string TournamentExternalId,
    string HomeParticipantExternalId,
    string AwayParticipantExternalId,
    DateTimeOffset StartsAt,
    EventStatus Status,
    int? FirstHalfHomeScore = null,
    int? FirstHalfAwayScore = null,
    int? FullTimeHomeScore = null,
    int? FullTimeAwayScore = null);
