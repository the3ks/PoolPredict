using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class TournamentCatalog
{
    private readonly List<Tournament> _tournaments;
    private readonly List<Event> _events;

    public TournamentCatalog()
    {
        var worldCupId = Ids.NewId();

        _tournaments =
        [
            new Tournament(
                worldCupId,
                "FIFA World Cup 2026",
                "Football",
                new DateOnly(2026, 6, 11),
                new DateOnly(2026, 7, 19))
        ];

        _events =
        [
            new Event(Ids.NewId(), worldCupId, "Mexico", "South Africa", new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero)),
            new Event(Ids.NewId(), worldCupId, "Canada", "TBD", new DateTimeOffset(2026, 6, 12, 1, 0, 0, TimeSpan.Zero)),
            new Event(Ids.NewId(), worldCupId, "United States", "TBD", new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero))
        ];
    }

    public IReadOnlyCollection<Tournament> GetTournaments() => _tournaments;

    public Tournament? GetTournament(Guid id) => _tournaments.SingleOrDefault(tournament => tournament.Id == id);

    public IReadOnlyCollection<Event> GetEvents(Guid tournamentId) =>
        _events.Where(matchEvent => matchEvent.TournamentId == tournamentId).ToArray();
}
