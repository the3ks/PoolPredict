using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Tournaments;

public sealed class Event : Entity
{
    public Event(Guid id, Guid tournamentId, string homeParticipant, string awayParticipant, DateTimeOffset startsAt)
        : base(id)
    {
        TournamentId = tournamentId;
        HomeParticipant = homeParticipant;
        AwayParticipant = awayParticipant;
        StartsAt = startsAt;
    }

    public Guid TournamentId { get; }

    public string HomeParticipant { get; }

    public string AwayParticipant { get; }

    public DateTimeOffset StartsAt { get; }

    public EventStatus Status { get; private set; } = EventStatus.Scheduled;
}
