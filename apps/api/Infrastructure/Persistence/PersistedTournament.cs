namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PersistedTournament
{
    public Guid Id { get; set; }

    public string ExternalId { get; set; } = "";

    public string Provider { get; set; } = "";

    public bool IsTestData { get; set; }

    public string Name { get; set; } = "";

    public string Sport { get; set; } = "";

    public DateOnly StartsOn { get; set; }

    public DateOnly EndsOn { get; set; }
}
