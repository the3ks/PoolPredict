using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedEvent
{
    public Guid Id { get; set; }

    public Guid TournamentId { get; set; }

    public Guid HomeParticipantId { get; set; }

    public Guid AwayParticipantId { get; set; }

    public string ExternalId { get; set; } = "";

    public string Provider { get; set; } = "";

    public bool IsTestData { get; set; }

    public EventManagementMode ManagementMode { get; set; } = EventManagementMode.Provider;

    public string HomeParticipant { get; set; } = "";

    public string AwayParticipant { get; set; } = "";

    public DateTimeOffset StartsAt { get; set; }

    public EventStatus Status { get; set; }
}
