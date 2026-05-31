using PoolPredict.Api.Domain.Common;

namespace PoolPredict.Api.Domain.Tournaments;

public sealed class Tournament : Entity
{
    public Tournament(Guid id, string name, string sport, DateOnly startsOn, DateOnly endsOn)
        : base(id)
    {
        Name = name;
        Sport = sport;
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    public string Name { get; }

    public string Sport { get; }

    public DateOnly StartsOn { get; }

    public DateOnly EndsOn { get; }
}
