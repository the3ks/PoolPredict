namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedParticipant
{
    public Guid Id { get; set; }

    public Guid TournamentId { get; set; }

    public string ExternalId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Code { get; set; } = "";

    public string Country { get; set; } = "";
}
