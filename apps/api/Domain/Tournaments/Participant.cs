using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Tournaments;

public sealed class Participant : Entity
{
    public Participant(Guid id, Guid tournamentId, string name, string code, string country)
        : base(id)
    {
        TournamentId = tournamentId;
        Name = name;
        Code = code;
        Country = country;
    }

    public Guid TournamentId { get; }

    public string Name { get; }

    public string Code { get; }

    public string Country { get; }
}
