using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Modules.Tournaments;

namespace PoolPredict.Api.Modules.Pools;

public sealed class PoolStore
{
    private const int PayoutConfigurationVersion = 1;

    private readonly List<Pool> _pools = [];
    private readonly List<Market> _markets = [];
    private readonly object _gate = new();

    public Pool CreatePool(CreatePoolRequest request, TournamentCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Pool name is required.", nameof(request));
        }

        if (request.StartingBalance <= 0)
        {
            throw new ArgumentException("Starting balance must be greater than zero.", nameof(request));
        }

        if (catalog.GetTournament(request.TournamentId) is null)
        {
            throw new ArgumentException("Tournament does not exist.", nameof(request));
        }

        lock (_gate)
        {
            var pool = new Pool(Ids.NewId(), request.Name.Trim(), request.TournamentId, request.Profile, request.StartingBalance);
            _pools.Add(pool);

            foreach (var matchEvent in catalog.GetEvents(request.TournamentId))
            {
                _markets.AddRange(GenerateMarkets(pool, matchEvent.Id));
            }

            return pool;
        }
    }

    public IReadOnlyCollection<Pool> GetPools()
    {
        lock (_gate)
        {
            return _pools.ToArray();
        }
    }

    public Pool? GetPool(Guid poolId)
    {
        lock (_gate)
        {
            return _pools.SingleOrDefault(pool => pool.Id == poolId);
        }
    }

    public IReadOnlyCollection<Market> GetMarkets(Guid poolId)
    {
        lock (_gate)
        {
            return _markets.Where(market => market.PoolId == poolId).ToArray();
        }
    }

    public Market? GetMarket(Guid marketId)
    {
        lock (_gate)
        {
            return _markets.SingleOrDefault(market => market.Id == marketId);
        }
    }

    private static IEnumerable<Market> GenerateMarkets(Pool pool, Guid eventId)
    {
        var periods = pool.Profile == MarketProfile.Casual
            ? [MarketPeriod.FullTime]
            : new[] { MarketPeriod.FullTime, MarketPeriod.FirstHalf };

        foreach (var period in periods)
        {
            yield return CreateMarket(pool.Id, eventId, MarketType.Winner, period, null, 2.0m);
            yield return CreateMarket(pool.Id, eventId, MarketType.CorrectScore, period, null, 5.0m);

            if (pool.Profile is MarketProfile.Standard or MarketProfile.Expert)
            {
                yield return CreateMarket(pool.Id, eventId, MarketType.Handicap, period, 0.5m, 2.0m);
                yield return CreateMarket(pool.Id, eventId, MarketType.OverUnder, period, 2.5m, 2.0m);
                yield return CreateMarket(pool.Id, eventId, MarketType.OddEven, period, null, 2.0m);
            }
        }
    }

    private static Market CreateMarket(
        Guid poolId,
        Guid eventId,
        MarketType type,
        MarketPeriod period,
        decimal? lineValue,
        decimal payoutMultiplier) =>
        new(Ids.NewId(), poolId, eventId, type, period, lineValue, payoutMultiplier, PayoutConfigurationVersion);
}
