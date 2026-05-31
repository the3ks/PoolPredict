using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Tournaments;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class EfTournamentPersistence(IDbContextFactory<PoolPredictDbContext> dbContextFactory) : ITournamentPersistence
{
    public async Task<TournamentSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var tournaments = await db.Tournaments.AsNoTracking().ToArrayAsync(cancellationToken);
        if (tournaments.Length == 0)
        {
            return null;
        }

        var participants = await db.Participants.AsNoTracking().ToArrayAsync(cancellationToken);
        var events = await db.Events.AsNoTracking().ToArrayAsync(cancellationToken);

        return new TournamentSnapshot(
            tournaments.Select(tournament => new Tournament(tournament.Id, tournament.Name, tournament.Sport, tournament.StartsOn, tournament.EndsOn)).ToArray(),
            participants.Select(participant => new Participant(participant.Id, participant.TournamentId, participant.Name, participant.Code, participant.Country)).ToArray(),
            events.Select(matchEvent => new Event(
                matchEvent.Id,
                matchEvent.TournamentId,
                matchEvent.HomeParticipantId,
                matchEvent.AwayParticipantId,
                matchEvent.HomeParticipant,
                matchEvent.AwayParticipant,
                matchEvent.StartsAt,
                matchEvent.Status)).ToArray());
    }

    public async Task SaveAsync(TournamentSyncSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Tournaments.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Tournaments.AddRange(snapshot.Tournaments.Select(item => new PersistedTournament
        {
            Id = item.Tournament.Id,
            ExternalId = item.ExternalId,
            Name = item.Tournament.Name,
            Sport = item.Tournament.Sport,
            StartsOn = item.Tournament.StartsOn,
            EndsOn = item.Tournament.EndsOn
        }));

        db.Participants.AddRange(snapshot.Participants.Select(item => new PersistedParticipant
        {
            Id = item.Participant.Id,
            TournamentId = item.Participant.TournamentId,
            ExternalId = item.ExternalId,
            Name = item.Participant.Name,
            Code = item.Participant.Code,
            Country = item.Participant.Country
        }));

        db.Events.AddRange(snapshot.Events.Select(item => new PersistedEvent
        {
            Id = item.Event.Id,
            TournamentId = item.Event.TournamentId,
            HomeParticipantId = item.Event.HomeParticipantId,
            AwayParticipantId = item.Event.AwayParticipantId,
            ExternalId = item.ExternalId,
            HomeParticipant = item.Event.HomeParticipant,
            AwayParticipant = item.Event.AwayParticipant,
            StartsAt = item.Event.StartsAt,
            Status = item.Event.Status
        }));

        await db.SaveChangesAsync(cancellationToken);
    }
}
