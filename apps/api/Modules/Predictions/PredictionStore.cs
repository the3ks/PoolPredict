using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Points;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Domain.Predictions;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Markets;
using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Modules.Tournaments;
using Microsoft.Extensions.Options;

namespace PoolPredict.Api.Modules.Predictions;

public sealed class PredictionStore
{
    private readonly List<Prediction> _predictions = [];
    private readonly List<PointLedgerEntry> _ledger = [];
    private readonly HashSet<(Guid PoolId, Guid MemberId)> _initializedMembers = [];
    private readonly IDbContextFactory<PoolPredictDbContext> _dbContextFactory;
    private readonly TimeSpan _handicapOpenWindow;
    private readonly object _gate = new();

    public PredictionStore(IDbContextFactory<PoolPredictDbContext> dbContextFactory, IOptions<MarketOptions> marketOptions)
    {
        _dbContextFactory = dbContextFactory;
        var openWindowHours = marketOptions.Value.HandicapOpenWindowHours <= 0
            ? 24
            : marketOptions.Value.HandicapOpenWindowHours;
        _handicapOpenWindow = TimeSpan.FromHours(openWindowHours);
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

        if (pool.PredictionsLocked)
        {
            throw new InvalidOperationException("Predictions are locked for this pool.");
        }

        if (request.Stake < pool.MinStake)
        {
            throw new InvalidOperationException($"Stake must be at least {pool.MinStake}.");
        }

        if (request.Stake > pool.MaxStake)
        {
            throw new InvalidOperationException($"Stake cannot exceed {pool.MaxStake}.");
        }

        if (market.PoolId != pool.Id)
        {
            throw new ArgumentException("Market does not belong to the selected pool.", nameof(request));
        }

        var now = DateTimeOffset.UtcNow;
        if (market.Status != MarketStatus.Open || matchEvent.StartsAt <= now)
        {
            throw new InvalidOperationException("This market is locked.");
        }

        if (market.Type == MarketType.Handicap)
        {
            if (market.LineValue is null)
            {
                throw new InvalidOperationException("Handicap line is not confirmed.");
            }

            if (now < matchEvent.StartsAt.Subtract(_handicapOpenWindow))
            {
                throw new InvalidOperationException($"Handicap predictions open {FormatOpenWindow(_handicapOpenWindow)} before kickoff.");
            }
        }

        ValidateSelectedOption(market, matchEvent, request.SelectedOption);

        lock (_gate)
        {
            EnsureMemberInitialized(pool.Id, member.Id, pool.StartingBalance);

            if (GetBalanceUnsafe(pool.Id, member.Id) < 0)
            {
                throw new InvalidOperationException("Member balance is negative. New predictions are blocked.");
            }

            var totalStakeForEvent = GetTotalStakeForEventUnsafe(pool.Id, member.Id, market.EventId);
            if (totalStakeForEvent + request.Stake > pool.MaxTotalStakePerEvent)
            {
                var remaining = Math.Max(0, pool.MaxTotalStakePerEvent - totalStakeForEvent);
                throw new InvalidOperationException(
                    $"This prediction exceeds the event cap of {pool.MaxTotalStakePerEvent}. Remaining allowance: {remaining}.");
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

    public IReadOnlyCollection<MarketPredictionSummaryResponse> GetMarketPredictionSummaries(Guid poolId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var rows = db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.PoolId == poolId)
            .Join(
                db.PoolMembers.AsNoTracking(),
                prediction => prediction.MemberId,
                member => member.Id,
                (prediction, member) => new { Prediction = prediction, Member = member })
            .Join(
                db.Users.AsNoTracking(),
                item => item.Member.UserId,
                user => user.Id,
                (item, user) => new
                {
                    item.Prediction.MarketId,
                    item.Prediction.SelectedOption,
                    DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName
                })
            .ToArray();

        return rows
            .GroupBy(row => new { row.MarketId, row.SelectedOption })
            .Select(group => new MarketPredictionSummaryResponse(
                group.Key.MarketId,
                group.Key.SelectedOption,
                group
                    .Select(row => row.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(displayName => displayName)
                    .ToArray()))
            .OrderBy(summary => summary.SelectedOption)
            .ToArray();
    }

    public IReadOnlyCollection<PredictionHistoryResponse> GetMemberPredictionHistory(Guid poolId, Guid memberId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var predictions = db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.PoolId == poolId && prediction.MemberId == memberId)
            .OrderByDescending(prediction => prediction.SubmittedAt)
            .ToArray();
        var predictionIds = predictions.Select(prediction => prediction.Id).ToList();
        var marketIds = predictions.Select(prediction => prediction.MarketId).Distinct().ToList();
        var markets = db.Markets
            .AsNoTracking()
            .Where(market => marketIds.Contains(market.Id))
            .ToDictionary(market => market.Id);
        var eventIds = markets.Values.Select(market => market.EventId).Distinct().ToList();
        var events = db.Events
            .AsNoTracking()
            .Where(matchEvent => eventIds.Contains(matchEvent.Id))
            .ToDictionary(matchEvent => matchEvent.Id);
        var settlementCredits = db.PointLedger
            .AsNoTracking()
            .Where(entry => entry.PredictionId != null
                && predictionIds.Contains(entry.PredictionId.Value)
                && (entry.Reason == PointLedgerReason.SettlementPayout
                    || entry.Reason == PointLedgerReason.SettlementRefund
                    || entry.Reason == PointLedgerReason.AdminCorrection))
            .GroupBy(entry => entry.PredictionId!.Value)
            .Select(group => new { PredictionId = group.Key, Points = group.Sum(entry => entry.Points) })
            .ToDictionary(item => item.PredictionId, item => item.Points);

        return predictions.Select(prediction =>
        {
            var market = markets.GetValueOrDefault(prediction.MarketId);
            var matchEvent = market is null ? null : events.GetValueOrDefault(market.EventId);
            var credit = settlementCredits.GetValueOrDefault(prediction.Id);
            var outcome = ResolveOutcome(prediction.Stake, credit, market?.Status);
            return new PredictionHistoryResponse(
                prediction.Id,
                prediction.PoolId,
                prediction.MemberId,
                prediction.MarketId,
                prediction.SelectedOption,
                prediction.Stake,
                prediction.MarketType,
                prediction.MarketPeriod,
                matchEvent is null ? null : $"{matchEvent.HomeParticipant} vs {matchEvent.AwayParticipant}",
                prediction.LineValueSnapshot,
                prediction.PayoutMultiplierSnapshot,
                prediction.PayoutConfigurationVersionSnapshot,
                prediction.SubmittedAt,
                market?.Status,
                outcome,
                credit,
                credit - prediction.Stake);
        }).ToArray();
    }

    public IReadOnlyCollection<LeaderboardEntryResponse> GetLeaderboard(Guid poolId, int startingBalance)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var members = db.PoolMembers
            .AsNoTracking()
            .Where(member => member.PoolId == poolId)
            .Join(
                db.Users.AsNoTracking(),
                member => member.UserId,
                user => user.Id,
                (member, user) => new { Member = member, User = user })
            .ToArray();
        var memberIds = members.Select(item => item.Member.Id).ToList();
        var balances = db.PointLedger
            .AsNoTracking()
            .Where(entry => entry.PoolId == poolId && memberIds.Contains(entry.MemberId))
            .GroupBy(entry => entry.MemberId)
            .Select(group => new { MemberId = group.Key, Balance = group.Sum(entry => entry.Points) })
            .ToDictionary(item => item.MemberId, item => item.Balance);
        var predictions = db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.PoolId == poolId && memberIds.Contains(prediction.MemberId))
            .ToArray();
        var predictionIds = predictions.Select(prediction => prediction.Id).ToList();
        var marketIds = predictions.Select(prediction => prediction.MarketId).Distinct().ToList();
        var marketStatuses = db.Markets
            .AsNoTracking()
            .Where(market => marketIds.Contains(market.Id))
            .ToDictionary(market => market.Id, market => market.Status);
        var settlementCredits = db.PointLedger
            .AsNoTracking()
            .Where(entry => entry.PredictionId != null
                && predictionIds.Contains(entry.PredictionId.Value)
                && (entry.Reason == PointLedgerReason.SettlementPayout
                    || entry.Reason == PointLedgerReason.SettlementRefund
                    || entry.Reason == PointLedgerReason.AdminCorrection))
            .GroupBy(entry => entry.PredictionId!.Value)
            .Select(group => new { PredictionId = group.Key, Points = group.Sum(entry => entry.Points) })
            .ToDictionary(item => item.PredictionId, item => item.Points);

        return members.Select(item =>
            {
                var memberPredictions = predictions.Where(prediction => prediction.MemberId == item.Member.Id).ToArray();
                var settledPredictions = memberPredictions
                    .Where(prediction => marketStatuses.GetValueOrDefault(prediction.MarketId) is MarketStatus.Settled or MarketStatus.Voided)
                    .ToArray();
                var wonPredictions = settledPredictions.Count(prediction =>
                    ResolveOutcome(
                        prediction.Stake,
                        settlementCredits.GetValueOrDefault(prediction.Id),
                        marketStatuses.GetValueOrDefault(prediction.MarketId)) is "Win" or "HalfWin");
                var settledStake = settledPredictions.Sum(prediction => prediction.Stake);
                var settledNet = settledPredictions.Sum(prediction =>
                    settlementCredits.GetValueOrDefault(prediction.Id) - prediction.Stake);
                var roi = settledStake == 0 ? 0m : Math.Round(settledNet / (decimal)settledStake * 100m, 2);

                return new LeaderboardEntryResponse(
                    item.Member.Id,
                    item.Member.UserId,
                    string.IsNullOrWhiteSpace(item.User.DisplayName) ? item.User.Email : item.User.DisplayName,
                    item.User.AvatarUrl,
                    item.Member.Role.ToString(),
                    balances.GetValueOrDefault(item.Member.Id, startingBalance),
                    memberPredictions.Length,
                    settledPredictions.Length,
                    wonPredictions,
                    settledPredictions.Length == 0 ? 0m : Math.Round(wonPredictions / (decimal)settledPredictions.Length * 100m, 2),
                    roi);
            })
            .OrderByDescending(entry => entry.Balance)
            .ThenByDescending(entry => entry.Roi)
            .ThenBy(entry => entry.DisplayName)
            .ToArray();
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

    public void InitializeMemberBalance(Guid poolId, Guid memberId, int startingBalance)
    {
        lock (_gate)
        {
            EnsureMemberInitialized(poolId, memberId, startingBalance);
        }
    }

    public void ApplyStartingBalanceAdjustment(Guid poolId, IReadOnlyCollection<PoolMember> members, int oldStartingBalance, int newStartingBalance)
    {
        var delta = newStartingBalance - oldStartingBalance;
        if (delta == 0)
        {
            return;
        }

        lock (_gate)
        {
            foreach (var member in members)
            {
                EnsureMemberInitialized(poolId, member.Id, oldStartingBalance);
            }

            var entries = members.Select(member => new PointLedgerEntry(
                Ids.NewId(),
                poolId,
                member.Id,
                delta,
                PointLedgerReason.StartingBalanceAdjustment,
                predictionId: null)).ToArray();

            _ledger.AddRange(entries);
            PersistLedgerEntries(entries);
        }
    }

    public void AddPersistedLedgerEntries(IReadOnlyCollection<PersistedPointLedgerEntry> entries)
    {
        lock (_gate)
        {
            foreach (var entry in entries)
            {
                if (_ledger.Any(candidate => candidate.Id == entry.Id))
                {
                    continue;
                }

                _ledger.Add(new PointLedgerEntry(
                    entry.Id,
                    entry.PoolId,
                    entry.MemberId,
                    entry.Points,
                    entry.Reason,
                    entry.PredictionId));
            }
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

    private int GetTotalStakeForEventUnsafe(Guid poolId, Guid memberId, Guid eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.PoolId == poolId && prediction.MemberId == memberId)
            .Join(
                db.Markets.AsNoTracking().Where(market => market.EventId == eventId),
                prediction => prediction.MarketId,
                market => market.Id,
                (prediction, _) => prediction.Stake)
            .Sum();
    }

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

    private void PersistLedgerEntries(IReadOnlyCollection<PointLedgerEntry> entries)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entryIds = entries.Select(entry => entry.Id).ToHashSet();
        var existingIds = db.PointLedger
            .Where(candidate => entryIds.Contains(candidate.Id))
            .Select(candidate => candidate.Id)
            .ToHashSet();
        var persisted = entries
            .Where(entry => !existingIds.Contains(entry.Id))
            .Select(ToPersistedLedgerEntry)
            .ToArray();
        if (persisted.Length == 0)
        {
            return;
        }

        db.PointLedger.AddRange(persisted);
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

    private static string ResolveOutcome(int stake, int settlementCredit, MarketStatus? marketStatus)
    {
        if (marketStatus is null or MarketStatus.Open or MarketStatus.Locked or MarketStatus.Draft or MarketStatus.LinePending)
        {
            return "Unsettled";
        }

        if (marketStatus == MarketStatus.Voided)
        {
            return "Cancelled";
        }

        if (settlementCredit == 0)
        {
            return "Lose";
        }

        if (settlementCredit < stake)
        {
            return "HalfLose";
        }

        if (settlementCredit == stake)
        {
            return "Push";
        }

        return settlementCredit < stake * 2 ? "HalfWin" : "Win";
    }

    private static string FormatOpenWindow(TimeSpan window) =>
        window.TotalHours == 1 ? "1 hour" : $"{window.TotalHours:0.##} hours";

    private static void ValidateSelectedOption(Market market, Domain.Tournaments.Event matchEvent, string selectedOption)
    {
        var trimmed = selectedOption.Trim();
        var isValid = market.Type switch
        {
            MarketType.OneXTwo => IsOneOf(trimmed, matchEvent.HomeParticipant, "Draw", matchEvent.AwayParticipant),
            MarketType.OverUnder => market.LineValue is decimal line
                && IsOneOf(trimmed, $"Over {FormatNumber(line)}", $"Under {FormatNumber(line)}"),
            MarketType.OddEven => IsOneOf(trimmed, "Odd", "Even"),
            MarketType.Handicap => market.LineValue is decimal line
                && IsOneOf(
                    trimmed,
                    $"{matchEvent.HomeParticipant} {FormatSignedNumber(line)}",
                    $"{matchEvent.AwayParticipant} {FormatSignedNumber(-line)}"),
            MarketType.CorrectScore => true,
            _ => false
        };

        if (!isValid)
        {
            throw new ArgumentException("Selected option is not valid for this market.");
        }
    }

    private static bool IsOneOf(string selectedOption, params string[] options) =>
        options.Any(option => string.Equals(selectedOption, option, StringComparison.OrdinalIgnoreCase));

    private static string FormatSignedNumber(decimal value) =>
        value > 0 ? $"+{FormatNumber(value)}" : FormatNumber(value);

    private static string FormatNumber(decimal value) =>
        value.ToString("0.##");
}

public sealed record PredictionHistoryResponse(
    Guid Id,
    Guid PoolId,
    Guid MemberId,
    Guid MarketId,
    string SelectedOption,
    int Stake,
    MarketType MarketType,
    MarketPeriod MarketPeriod,
    string? EventName,
    decimal? LineValueSnapshot,
    decimal PayoutMultiplierSnapshot,
    int PayoutConfigurationVersionSnapshot,
    DateTimeOffset SubmittedAt,
    MarketStatus? MarketStatus,
    string Outcome,
    int SettlementCredit,
    int NetPoints);

public sealed record LeaderboardEntryResponse(
    Guid MemberId,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    int Balance,
    int PredictionCount,
    int SettledPredictionCount,
    int WinCount,
    decimal WinRate,
    decimal Roi);

public sealed record MarketPredictionSummaryResponse(
    Guid MarketId,
    string SelectedOption,
    IReadOnlyCollection<string> Users);
