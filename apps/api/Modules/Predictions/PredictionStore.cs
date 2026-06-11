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
using PoolPredict.Api.Domain.Tournaments;

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

            if (market.Type == MarketType.OneXTwo && HasConflictingOneXTwoPredictionForEventUnsafe(pool.Id, member.Id, market.EventId, request.SelectedOption))
            {
                throw new InvalidOperationException("For 1X2, you can only choose one option per event. You can add more points to the same option, but cannot switch to another option.");
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

    public AutoPickPreviewResponse PreviewAutoPick(Guid poolId, AutoPickPredictionsRequest request, Guid userId, PoolStore pools, TournamentCatalog catalog)
    {
        if (request.Stake <= 0)
        {
            throw new ArgumentException("Stake must be greater than zero.", nameof(request));
        }

        var pool = pools.GetPool(poolId) ?? throw new ArgumentException("Pool does not exist.", nameof(poolId));
        var member = pools.GetMember(poolId, userId) ?? throw new UnauthorizedAccessException("You are not a member of this pool.");

        lock (_gate)
        {
            var plan = BuildAutoPickPlanUnsafe(pool, member, request.Stake, pools, catalog);
            return ToAutoPickPreviewResponse(plan);
        }
    }

    public AutoPickSubmissionResponse SubmitAutoPick(Guid poolId, AutoPickPredictionsRequest request, Guid userId, PoolStore pools, TournamentCatalog catalog)
    {
        if (request.Stake <= 0)
        {
            throw new ArgumentException("Stake must be greater than zero.", nameof(request));
        }

        var pool = pools.GetPool(poolId) ?? throw new ArgumentException("Pool does not exist.", nameof(poolId));
        var member = pools.GetMember(poolId, userId) ?? throw new UnauthorizedAccessException("You are not a member of this pool.");

        lock (_gate)
        {
            var plan = BuildAutoPickPlanUnsafe(pool, member, request.Stake, pools, catalog);
            if (!plan.HasEnoughBalance)
            {
                throw new InvalidOperationException(
                    $"Auto pick needs {plan.TotalStake} total stake, but current balance is {plan.CurrentBalance}.");
            }

            if (plan.EligibleEvents.Count == 0)
            {
                throw new InvalidOperationException("No eligible events available for auto pick.");
            }

            var predictions = new List<Prediction>(plan.EligibleEvents.Count);
            var ledgerEntries = new List<PointLedgerEntry>(plan.EligibleEvents.Count);
            var eligibleEvents = plan.EligibleEvents.ToArray();

            foreach (var eligibleEvent in eligibleEvents)
            {
                var option = PickRandomOption(eligibleEvent.Market, eligibleEvent.Event);
                var prediction = new Prediction(
                    Ids.NewId(),
                    pool.Id,
                    member.Id,
                    eligibleEvent.Market.Id,
                    option,
                    plan.Stake,
                    eligibleEvent.Market.Type,
                    eligibleEvent.Market.Period,
                    eligibleEvent.Market.LineValue,
                    eligibleEvent.Market.PayoutMultiplier,
                    eligibleEvent.Market.PayoutConfigurationVersion);
                var stakeLedgerEntry = new PointLedgerEntry(
                    Ids.NewId(),
                    pool.Id,
                    member.Id,
                    -plan.Stake,
                    PointLedgerReason.PredictionSubmitted,
                    prediction.Id);

                _predictions.Add(prediction);
                _ledger.Add(stakeLedgerEntry);
                predictions.Add(prediction);
                ledgerEntries.Add(stakeLedgerEntry);
            }

            PersistPredictionBatch(predictions, ledgerEntries);

            return new AutoPickSubmissionResponse(
                plan.Stake,
                plan.EligibleEvents.Count,
                plan.SkippedEvents.Count,
                plan.TotalStake,
                plan.CurrentBalance,
                plan.CurrentBalance - plan.TotalStake,
                predictions.Select((prediction, index) => new AutoPickCreatedPredictionResponse(
                    prediction.Id,
                    eligibleEvents[index].Event.Id,
                    FormatEventName(eligibleEvents[index].Event),
                    prediction.MarketId,
                    prediction.MarketType,
                    prediction.MarketPeriod,
                    prediction.SelectedOption,
                    prediction.Stake,
                    prediction.SubmittedAt)).ToArray(),
                plan.SkippedEvents);
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
                    settledNet,
                    memberPredictions.Length,
                    settledPredictions.Length,
                    wonPredictions,
                    settledPredictions.Length == 0 ? 0m : Math.Round(wonPredictions / (decimal)settledPredictions.Length * 100m, 2),
                    roi);
            })
            .OrderByDescending(entry => entry.WinLoss)
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

    private int GetTotalStakeForEventUnsafe(Guid poolId, Guid memberId, Guid eventId, IReadOnlyDictionary<Guid, Market> marketsById) =>
        _predictions
            .Where(prediction =>
                prediction.PoolId == poolId
                && prediction.MemberId == memberId
                && marketsById.TryGetValue(prediction.MarketId, out var market)
                && market.EventId == eventId)
            .Sum(prediction => prediction.Stake);

    private bool HasAnyPredictionForEventUnsafe(Guid poolId, Guid memberId, Guid eventId, IReadOnlyDictionary<Guid, Market> marketsById) =>
        _predictions.Any(prediction =>
            prediction.PoolId == poolId
            && prediction.MemberId == memberId
            && marketsById.TryGetValue(prediction.MarketId, out var market)
            && market.EventId == eventId);

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

    private bool HasConflictingOneXTwoPredictionForEventUnsafe(Guid poolId, Guid memberId, Guid eventId, string selectedOption)
    {
        var normalizedOption = selectedOption.Trim();
        using var db = _dbContextFactory.CreateDbContext();
        var existingOptions = db.Predictions
            .AsNoTracking()
            .Where(prediction => prediction.PoolId == poolId && prediction.MemberId == memberId)
            .Join(
                db.Markets.AsNoTracking().Where(market => market.EventId == eventId && market.Type == MarketType.OneXTwo),
                prediction => prediction.MarketId,
                market => market.Id,
                (prediction, _) => prediction.SelectedOption)
            .ToArray();

        return existingOptions.Any(existingOption => !string.Equals(existingOption, normalizedOption, StringComparison.OrdinalIgnoreCase));
    }

    private AutoPickPlan BuildAutoPickPlanUnsafe(Pool pool, PoolMember member, int stake, PoolStore pools, TournamentCatalog catalog)
    {
        if (pool.PredictionsLocked)
        {
            throw new InvalidOperationException("Predictions are locked for this pool.");
        }

        if (stake < pool.MinStake)
        {
            throw new InvalidOperationException($"Stake must be at least {pool.MinStake}.");
        }

        if (stake > pool.MaxStake)
        {
            throw new InvalidOperationException($"Stake cannot exceed {pool.MaxStake}.");
        }

        EnsureMemberInitialized(pool.Id, member.Id, pool.StartingBalance);

        var currentBalance = GetBalanceUnsafe(pool.Id, member.Id);
        var now = DateTimeOffset.UtcNow;
        var markets = pools.GetMarkets(pool.Id);
        var marketsById = markets.ToDictionary(market => market.Id);
        var marketsByEvent = markets
            .GroupBy(market => market.EventId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<Market>)group.ToArray());

        var eligibleEvents = new List<AutoPickEligibleEvent>();
        var skippedEvents = new List<AutoPickSkippedEventResponse>();

        foreach (var matchEvent in catalog.GetEvents(pool.TournamentId).OrderBy(matchEvent => matchEvent.StartsAt))
        {
            var skipReason = GetAutoPickSkipReasonUnsafe(pool, member, matchEvent, stake, now, marketsById, marketsByEvent);
            if (skipReason is not null)
            {
                skippedEvents.Add(new AutoPickSkippedEventResponse(
                    matchEvent.Id,
                    FormatEventName(matchEvent),
                    skipReason));
                continue;
            }

            var eligibleMarkets = marketsByEvent[matchEvent.Id]
                .Where(market => IsMarketEligibleForAutoPick(market, matchEvent, now))
                .ToArray();
            var chosenMarket = eligibleMarkets[Random.Shared.Next(eligibleMarkets.Length)];
            eligibleEvents.Add(new AutoPickEligibleEvent(matchEvent, chosenMarket));
        }

        var totalStake = eligibleEvents.Count * stake;
        return new AutoPickPlan(
            pool,
            member,
            stake,
            currentBalance,
            eligibleEvents,
            skippedEvents,
            totalStake,
            totalStake <= currentBalance);
    }

    private string? GetAutoPickSkipReasonUnsafe(
        Pool pool,
        PoolMember member,
        Event matchEvent,
        int stake,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, Market> marketsById,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Market>> marketsByEvent)
    {
        if (matchEvent.Status != EventStatus.Scheduled)
        {
            return "Event is not scheduled.";
        }

        if (matchEvent.StartsAt <= now)
        {
            return "Kickoff has passed.";
        }

        if (!marketsByEvent.TryGetValue(matchEvent.Id, out var eventMarkets))
        {
            return "No markets available.";
        }

        if (HasAnyPredictionForEventUnsafe(pool.Id, member.Id, matchEvent.Id, marketsById))
        {
            return "Already predicted.";
        }

        var eventStakeUsed = GetTotalStakeForEventUnsafe(pool.Id, member.Id, matchEvent.Id, marketsById);
        if (eventStakeUsed + stake > pool.MaxTotalStakePerEvent)
        {
            return "Event cap too low.";
        }

        return eventMarkets.Any(market => IsMarketEligibleForAutoPick(market, matchEvent, now))
            ? null
            : "No open market.";
    }

    private bool IsMarketEligibleForAutoPick(Market market, Event matchEvent, DateTimeOffset now)
    {
        if (market.Status != MarketStatus.Open || matchEvent.StartsAt <= now)
        {
            return false;
        }

        if (market.Type == MarketType.Handicap)
        {
            return market.LineValue is not null && now >= matchEvent.StartsAt.Subtract(_handicapOpenWindow);
        }

        if (market.Type == MarketType.OverUnder)
        {
            return market.LineValue is not null;
        }

        return true;
    }

    private static AutoPickPreviewResponse ToAutoPickPreviewResponse(AutoPickPlan plan) =>
        new(
            plan.Stake,
            plan.EligibleEvents.Count,
            plan.SkippedEvents.Count,
            plan.TotalStake,
            plan.CurrentBalance,
            plan.CurrentBalance - plan.TotalStake,
            plan.HasEnoughBalance,
            plan.EligibleEvents.Select(item => new AutoPickEligibleEventResponse(
                item.Event.Id,
                FormatEventName(item.Event),
                item.Market.Id,
                item.Market.Type,
                item.Market.Period)).ToArray(),
            plan.SkippedEvents);

    private static string PickRandomOption(Market market, Event matchEvent)
    {
        var options = market.Type switch
        {
            MarketType.OneXTwo => new[]
            {
                matchEvent.HomeParticipant,
                "Draw",
                matchEvent.AwayParticipant
            },
            MarketType.OverUnder when market.LineValue is decimal line => new[]
            {
                $"Over {FormatNumber(line)}",
                $"Under {FormatNumber(line)}"
            },
            MarketType.OddEven => new[] { "Odd", "Even" },
            MarketType.Handicap when market.LineValue is decimal line => new[]
            {
                $"{matchEvent.HomeParticipant} {FormatSignedNumber(line)}",
                $"{matchEvent.AwayParticipant} {FormatSignedNumber(-line)}"
            },
            MarketType.CorrectScore => new[]
            {
                "0-0",
                "1-0",
                "0-1",
                "1-1",
                "2-0",
                "0-2",
                "2-1",
                "1-2",
                "2-2",
                "3-1",
                "1-3"
            },
            _ => Array.Empty<string>()
        };

        if (options.Length == 0)
        {
            throw new InvalidOperationException("No valid option available for auto pick.");
        }

        return options[Random.Shared.Next(options.Length)];
    }

    private static string FormatEventName(Event matchEvent) =>
        $"{matchEvent.HomeParticipant} vs {matchEvent.AwayParticipant}";

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

    private void PersistPredictionBatch(IReadOnlyCollection<Prediction> predictions, IReadOnlyCollection<PointLedgerEntry> ledgerEntries)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.Predictions.AddRange(predictions.Select(prediction => new PersistedPrediction
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
        }));
        db.PointLedger.AddRange(ledgerEntries.Select(ToPersistedLedgerEntry));
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
    int WinLoss,
    int PredictionCount,
    int SettledPredictionCount,
    int WinCount,
    decimal WinRate,
    decimal Roi);

public sealed record MarketPredictionSummaryResponse(
    Guid MarketId,
    string SelectedOption,
    IReadOnlyCollection<string> Users);

public sealed record AutoPickPreviewResponse(
    int Stake,
    int EligibleEventCount,
    int SkippedEventCount,
    int TotalStake,
    int CurrentBalance,
    int BalanceAfterAutoPick,
    bool HasEnoughBalance,
    IReadOnlyCollection<AutoPickEligibleEventResponse> EligibleEvents,
    IReadOnlyCollection<AutoPickSkippedEventResponse> SkippedEvents);

public sealed record AutoPickSubmissionResponse(
    int Stake,
    int CreatedCount,
    int SkippedCount,
    int TotalStake,
    int CurrentBalanceBefore,
    int BalanceAfter,
    IReadOnlyCollection<AutoPickCreatedPredictionResponse> CreatedPredictions,
    IReadOnlyCollection<AutoPickSkippedEventResponse> SkippedEvents);

public sealed record AutoPickEligibleEventResponse(
    Guid EventId,
    string EventName,
    Guid MarketId,
    MarketType MarketType,
    MarketPeriod MarketPeriod);

public sealed record AutoPickSkippedEventResponse(
    Guid EventId,
    string EventName,
    string Reason);

public sealed record AutoPickCreatedPredictionResponse(
    Guid PredictionId,
    Guid EventId,
    string EventName,
    Guid MarketId,
    MarketType MarketType,
    MarketPeriod MarketPeriod,
    string SelectedOption,
    int Stake,
    DateTimeOffset SubmittedAt);

sealed record AutoPickEligibleEvent(
    Event Event,
    Market Market);

sealed record AutoPickPlan(
    Pool Pool,
    PoolMember Member,
    int Stake,
    int CurrentBalance,
    IReadOnlyCollection<AutoPickEligibleEvent> EligibleEvents,
    IReadOnlyCollection<AutoPickSkippedEventResponse> SkippedEvents,
    int TotalStake,
    bool HasEnoughBalance);
