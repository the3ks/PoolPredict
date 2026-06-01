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
                matchEvent.Status)).ToArray(),
            tournaments.ToDictionary(tournament => tournament.ExternalId, tournament => tournament.Id, StringComparer.OrdinalIgnoreCase),
            participants.ToDictionary(participant => participant.ExternalId, participant => participant.Id, StringComparer.OrdinalIgnoreCase),
            events.ToDictionary(matchEvent => matchEvent.ExternalId, matchEvent => matchEvent.Id, StringComparer.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(TournamentSyncSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var item in snapshot.Tournaments)
        {
            var persisted = await db.Tournaments.SingleOrDefaultAsync(tournament => tournament.ExternalId == item.ExternalId, cancellationToken);
            if (persisted is null)
            {
                persisted = new PersistedTournament { Id = item.Tournament.Id, ExternalId = item.ExternalId };
                db.Tournaments.Add(persisted);
            }

            persisted.Name = item.Tournament.Name;
            persisted.Sport = item.Tournament.Sport;
            persisted.StartsOn = item.Tournament.StartsOn;
            persisted.EndsOn = item.Tournament.EndsOn;
        }

        foreach (var item in snapshot.Participants)
        {
            var persisted = await db.Participants.SingleOrDefaultAsync(participant => participant.ExternalId == item.ExternalId, cancellationToken);
            if (persisted is null)
            {
                persisted = new PersistedParticipant { Id = item.Participant.Id, ExternalId = item.ExternalId };
                db.Participants.Add(persisted);
            }

            persisted.TournamentId = item.Participant.TournamentId;
            persisted.Name = item.Participant.Name;
            persisted.Code = item.Participant.Code;
            persisted.Country = item.Participant.Country;
        }

        foreach (var item in snapshot.Events)
        {
            var persisted = await db.Events.SingleOrDefaultAsync(matchEvent => matchEvent.ExternalId == item.ExternalId, cancellationToken);
            if (persisted is null)
            {
                persisted = new PersistedEvent { Id = item.Event.Id, ExternalId = item.ExternalId };
                db.Events.Add(persisted);
            }

            persisted.TournamentId = item.Event.TournamentId;
            persisted.HomeParticipantId = item.Event.HomeParticipantId;
            persisted.AwayParticipantId = item.Event.AwayParticipantId;
            persisted.HomeParticipant = item.Event.HomeParticipant;
            persisted.AwayParticipant = item.Event.AwayParticipant;
            persisted.StartsAt = item.Event.StartsAt;
            persisted.Status = item.Event.Status;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
