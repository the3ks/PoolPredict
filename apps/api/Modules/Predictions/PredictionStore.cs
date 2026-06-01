using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Points;
using PoolPredict.Api.Domain.Predictions;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Modules.Tournaments;

namespace PoolPredict.Api.Modules.Predictions;

public sealed class PredictionStore
{
    private readonly List<Prediction> _predictions = [];
    private readonly List<PointLedgerEntry> _ledger = [];
    private readonly HashSet<(Guid PoolId, Guid MemberId)> _initializedMembers = [];
    private readonly IDbContextFactory<PoolPredictDbContext> _dbContextFactory;
    private readonly object _gate = new();

    public PredictionStore(IDbContextFactory<PoolPredictDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        LoadPersisted();
    }

    public Prediction Submit(SubmitPredictionRequest request, Guid userId, PoolStore pools, TournamentCatalog catalog)
    {
        if (request.Stake <= 0)
        {
            throw new ArgumentException("Stake must be greater than zero.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SelectedOption))
        {
            throw new ArgumentException("Selected option is required.", nameof(request));
        }

        var pool = pools.GetPool(request.PoolId) ?? throw new ArgumentException("Pool does not exist.", nameof(request));
        var member = pools.GetMember(pool.Id, userId) ?? throw new UnauthorizedAccessException("You are not a member of this pool.");
        var market = pools.GetMarket(request.MarketId) ?? throw new ArgumentException("Market does not exist.", nameof(request));
        var matchEvent = catalog.GetEvent(market.EventId) ?? throw new ArgumentException("Market event does not exist.", nameof(request));

        if (market.PoolId != pool.Id)
        {
            throw new ArgumentException("Market does not belong to the selected pool.", nameof(request));
        }

        if (market.Status != MarketStatus.Open || matchEvent.StartsAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("This market is locked.");
        }

        lock (_gate)
        {
            EnsureMemberInitialized(pool.Id, member.Id, pool.StartingBalance);

            if (GetBalanceUnsafe(pool.Id, member.Id) < 0)
            {
                throw new InvalidOperationException("Member balance is negative. New predictions are blocked.");
            }

            var prediction = new Prediction(
                Ids.NewId(),
                pool.Id,
                member.Id,
                market.Id,
                request.SelectedOption.Trim(),
                request.Stake,
                market.Type,
                market.Period,
                market.LineValue,
                market.PayoutMultiplier,
                market.PayoutConfigurationVersion);

            _predictions.Add(prediction);
            var stakeLedgerEntry = new PointLedgerEntry(
                Ids.NewId(),
                pool.Id,
                member.Id,
                -request.Stake,
                PointLedgerReason.PredictionSubmitted,
                prediction.Id);

            _ledger.Add(stakeLedgerEntry);
            PersistPredictionSlice(prediction, stakeLedgerEntry);

            return prediction;
        }
    }

    public IReadOnlyCollection<Prediction> GetPoolPredictions(Guid poolId)
    {
        lock (_gate)
        {
            return _predictions.Where(prediction => prediction.PoolId == poolId).ToArray();
        }
    }

    public IReadOnlyCollection<Prediction> GetMemberPredictions(Guid poolId, Guid memberId)
    {
        lock (_gate)
        {
            return _predictions
                .Where(prediction => prediction.PoolId == poolId && prediction.MemberId == memberId)
                .OrderByDescending(prediction => prediction.SubmittedAt)
                .ToArray();
        }
    }

    public int GetBalance(Guid poolId, Guid memberId)
    {
        lock (_gate)
        {
            return GetBalanceUnsafe(poolId, memberId);
        }
    }

    public int GetBalance(PoolDetailsResponse pool)
    {
        lock (_gate)
        {
            EnsureMemberInitialized(pool.Id, pool.MemberId, pool.StartingBalance);
            return GetBalanceUnsafe(pool.Id, pool.MemberId);
        }
    }

    private void EnsureMemberInitialized(Guid poolId, Guid memberId, int startingBalance)
    {
        if (!_initializedMembers.Add((poolId, memberId)))
        {
            return;
        }

        var entry = new PointLedgerEntry(
            Ids.NewId(),
            poolId,
            memberId,
            startingBalance,
            PointLedgerReason.StartingBalance,
            predictionId: null);

        _ledger.Add(entry);
        PersistLedgerEntry(entry);
    }

    private int GetBalanceUnsafe(Guid poolId, Guid memberId) =>
        _ledger
            .Where(entry => entry.PoolId == poolId && entry.MemberId == memberId)
            .Sum(entry => entry.Points);

    private void LoadPersisted()
    {
        using var db = _dbContextFactory.CreateDbContext();

        _predictions.AddRange(db.Predictions.AsNoTracking().Select(prediction => new Prediction(
            prediction.Id,
            prediction.PoolId,
            prediction.MemberId,
            prediction.MarketId,
            prediction.SelectedOption,
            prediction.Stake,
            prediction.MarketType,
            prediction.MarketPeriod,
            prediction.LineValueSnapshot,
            prediction.PayoutMultiplierSnapshot,
            prediction.PayoutConfigurationVersionSnapshot)));

        _ledger.AddRange(db.PointLedger.AsNoTracking().Select(entry => new PointLedgerEntry(
            entry.Id,
            entry.PoolId,
            entry.MemberId,
            entry.Points,
            entry.Reason,
            entry.PredictionId)));

        foreach (var entry in _ledger.Where(entry => entry.Reason == PointLedgerReason.StartingBalance))
        {
            _initializedMembers.Add((entry.PoolId, entry.MemberId));
        }
    }

    private void PersistPredictionSlice(Prediction prediction, PointLedgerEntry ledgerEntry)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.Predictions.Add(new PersistedPrediction
        {
            Id = prediction.Id,
            PoolId = prediction.PoolId,
            MemberId = prediction.MemberId,
            MarketId = prediction.MarketId,
            SelectedOption = prediction.SelectedOption,
            Stake = prediction.Stake,
            MarketType = prediction.MarketType,
            MarketPeriod = prediction.MarketPeriod,
            LineValueSnapshot = prediction.LineValueSnapshot,
            PayoutMultiplierSnapshot = prediction.PayoutMultiplierSnapshot,
            PayoutConfigurationVersionSnapshot = prediction.PayoutConfigurationVersionSnapshot,
            SubmittedAt = prediction.SubmittedAt
        });
        db.PointLedger.Add(ToPersistedLedgerEntry(ledgerEntry));
        db.SaveChanges();
    }

    private void PersistLedgerEntry(PointLedgerEntry entry)
    {
        using var db = _dbContextFactory.CreateDbContext();
        if (db.PointLedger.Any(candidate => candidate.Id == entry.Id))
        {
            return;
        }

        db.PointLedger.Add(ToPersistedLedgerEntry(entry));
        db.SaveChanges();
    }

    private static PersistedPointLedgerEntry ToPersistedLedgerEntry(PointLedgerEntry entry) => new()
    {
        Id = entry.Id,
        PoolId = entry.PoolId,
        MemberId = entry.MemberId,
        Points = entry.Points,
        Reason = entry.Reason,
        PredictionId = entry.PredictionId,
        CreatedAt = entry.CreatedAt
    };
}
