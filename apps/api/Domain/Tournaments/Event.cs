using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Tournaments;

public sealed class Event : Entity
{
    public Event(
        Guid id,
        Guid tournamentId,
        Guid homeParticipantId,
        Guid awayParticipantId,
        string homeParticipant,
        string awayParticipant,
        DateTimeOffset startsAt,
        EventStatus status = EventStatus.Scheduled)
        : base(id)
    {
        TournamentId = tournamentId;
        HomeParticipantId = homeParticipantId;
        AwayParticipantId = awayParticipantId;
        HomeParticipant = homeParticipant;
        AwayParticipant = awayParticipant;
        StartsAt = startsAt;
        Status = status;
    }

    public Guid TournamentId { get; }

    public Guid HomeParticipantId { get; }

    public Guid AwayParticipantId { get; }

    public string HomeParticipant { get; }

    public string AwayParticipant { get; }

    public DateTimeOffset StartsAt { get; }

    public EventStatus Status { get; private set; }
}
