namespace PoolPredict.Api.Modules.Tournaments;

public sealed class MockEventProvider : IEventProvider
{
    public const string WorldCupExternalId = "fifa-world-cup-2026";

    private static readonly TournamentDto WorldCup = new(
        WorldCupExternalId,
        "FIFA World Cup 2026",
        "Football",
        new DateOnly(2026, 6, 11),
        new DateOnly(2026, 7, 19));

    private static readonly ParticipantDto[] Participants =
    [
        new("mex", WorldCupExternalId, "Mexico", "MEX", "Mexico"),
        new("rsa", WorldCupExternalId, "South Africa", "RSA", "South Africa"),
        new("can", WorldCupExternalId, "Canada", "CAN", "Canada"),
        new("tbd-1", WorldCupExternalId, "TBD", "TBD", "To be determined"),
        new("usa", WorldCupExternalId, "United States", "USA", "United States"),
        new("tbd-2", WorldCupExternalId, "TBD", "TBD", "To be determined")
    ];

    private static readonly EventDto[] Events =
    [
        new("wc-2026-match-001", WorldCupExternalId, "mex", "rsa", new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero), Domain.Tournaments.EventStatus.Scheduled),
        new("wc-2026-match-002", WorldCupExternalId, "can", "tbd-1", new DateTimeOffset(2026, 6, 12, 1, 0, 0, TimeSpan.Zero), Domain.Tournaments.EventStatus.Scheduled),
        new("wc-2026-match-003", WorldCupExternalId, "usa", "tbd-2", new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero), Domain.Tournaments.EventStatus.Scheduled)
    ];

    public Task SyncTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IEnumerable<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TournamentDto>>([WorldCup]);

    public Task<IEnumerable<ParticipantDto>> GetParticipantsAsync(string tournamentExternalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Participants.Where(participant => participant.TournamentExternalId == tournamentExternalId));

    public Task<IEnumerable<EventDto>> GetEventsAsync(string tournamentExternalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Events.Where(matchEvent => matchEvent.TournamentExternalId == tournamentExternalId));

    public Task<IEnumerable<EventDto>> GetLiveEventsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Events.Where(matchEvent => matchEvent.Status == Domain.Tournaments.EventStatus.Live));

    public Task<EventDto?> GetEventAsync(string externalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Events.SingleOrDefault(matchEvent => matchEvent.ExternalId == externalId));
}
