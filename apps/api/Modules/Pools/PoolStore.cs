using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Markets;
using PoolPredict.Api.Modules.Tournaments;
using System.Security.Cryptography;

namespace PoolPredict.Api.Modules.Pools;

public sealed class PoolStore
{
    private const decimal CurrentOneXTwoPayoutMultiplier = 2.5m;
    private readonly List<Pool> _pools = [];
    private readonly List<PoolMember> _members = [];
    private readonly List<PoolInvite> _invites = [];
    private readonly List<Market> _markets = [];
    private readonly PayoutConfigurationStore _payoutConfigurations;
    private readonly IDbContextFactory<PoolPredictDbContext> _dbContextFactory;
    private readonly object _gate = new();

    public PoolStore(PayoutConfigurationStore payoutConfigurations, IDbContextFactory<PoolPredictDbContext> dbContextFactory)
    {
        _payoutConfigurations = payoutConfigurations;
        _dbContextFactory = dbContextFactory;
        EnsureCurrentOneXTwoMarketPayouts();
        LoadPersisted();
    }

    public Pool CreatePool(Guid ownerUserId, CreatePoolRequest request, TournamentCatalog catalog)
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

        var stakeSettings = CreateDefaultStakeSettings(request.StartingBalance);

        lock (_gate)
        {
            var pool = new Pool(
                Ids.NewId(),
                ownerUserId,
                request.Name.Trim(),
                request.TournamentId,
                request.Profile,
                request.StartingBalance,
                predictionsLocked: false,
                coverImageUrl: null,
                defaultStake: stakeSettings.defaultStake,
                minStake: stakeSettings.minStake,
                maxStake: stakeSettings.maxStake,
                maxTotalStakePerEvent: stakeSettings.maxTotalStakePerEvent);
            var owner = new PoolMember(Ids.NewId(), pool.Id, ownerUserId, PoolMemberRole.Owner, DateTimeOffset.UtcNow);
            var generatedMarkets = catalog.GetEvents(request.TournamentId)
                .SelectMany(matchEvent => GenerateMarkets(pool, matchEvent.Id, _payoutConfigurations.GetActiveConfiguration()))
                .ToArray();

            _pools.Add(pool);
            _members.Add(owner);
            _markets.AddRange(generatedMarkets);
            PersistPoolSlice(pool, owner, generatedMarkets);


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

    public IReadOnlyCollection<PoolSummaryResponse> GetPoolsForUser(Guid userId)
    {
        lock (_gate)
        {
            return _members
                .Where(member => member.UserId == userId)
                .Join(
                    _pools.Where(pool => !pool.IsHidden),
                    member => member.PoolId,
                    pool => pool.Id,
                    (member, pool) => ToSummary(pool, member))
                .ToArray();
        }
    }

    public Pool? GetPool(Guid poolId)
    {
        lock (_gate)
        {
            return _pools.SingleOrDefault(pool => pool.Id == poolId);
        }
    }

    public PoolDetailsResponse? GetPoolForUser(Guid poolId, Guid userId)
    {
        lock (_gate)
        {
            var membership = _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == userId);
            var pool = _pools.SingleOrDefault(candidate => candidate.Id == poolId);
            return pool is null || pool.IsHidden || membership is null
                ? null
                : ToDetails(pool, membership);
        }
    }

    public Pool UpdatePool(Guid poolId, Guid userId, UpdatePoolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Pool name is required.", nameof(request));
        }

        if (request.StartingBalance <= 0)
        {
            throw new ArgumentException("Starting balance must be greater than zero.", nameof(request));
        }

        if (request.LeaderboardMinEventAverageStakePercent is < 0m or > 100m)
        {
            throw new ArgumentException("Leaderboard minimum event average stake percent must be between 0 and 100.", nameof(request));
        }

        var normalizedStakeSettings = NormalizeStakeSettings(
            request.StartingBalance,
            request.DefaultStake,
            request.MinStake,
            request.MaxStake,
            request.MaxTotalStakePerEvent);
        ValidateStakeSettings(
            normalizedStakeSettings.defaultStake,
            normalizedStakeSettings.minStake,
            normalizedStakeSettings.maxStake,
            normalizedStakeSettings.maxTotalStakePerEvent);
        var coverImageUrl = NormalizeOptionalUrl(request.CoverImageUrl, "Cover image URL");
        var announcementTitle = NormalizeAnnouncementTitle(request.AnnouncementTitle);

        lock (_gate)
        {
            var pool = _pools.SingleOrDefault(candidate => candidate.Id == poolId)
                ?? throw new KeyNotFoundException("Pool does not exist.");

            EnsureCanManage(poolId, userId);
            pool.UpdateSettings(
                request.Name.Trim(),
                request.StartingBalance,
                request.PredictionsLocked,
                coverImageUrl,
                normalizedStakeSettings.defaultStake,
                normalizedStakeSettings.minStake,
                normalizedStakeSettings.maxStake,
                normalizedStakeSettings.maxTotalStakePerEvent,
                request.VipEventStakeMultiplierEnabled,
                request.LeaderboardMinEventAverageStakePercent,
                announcementTitle);
            PersistPoolUpdate(pool);
            return pool;
        }
    }

    public Pool SetPoolHidden(Guid poolId, bool isHidden)
    {
        lock (_gate)
        {
            var pool = _pools.SingleOrDefault(candidate => candidate.Id == poolId)
                ?? throw new KeyNotFoundException("Pool does not exist.");

            pool.SetHidden(isHidden);
            PersistPoolUpdate(pool);
            return pool;
        }
    }

    public PoolInvite CreateInvite(Guid poolId, Guid userId)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            EnsureCanManageInvites(poolId, userId);

            var invite = new PoolInvite(Ids.NewId(), poolId, userId, CreateInviteCode(), DateTimeOffset.UtcNow);
            _invites.Add(invite);
            PersistInvite(invite);
            return invite;
        }
    }

    public IReadOnlyCollection<PoolInviteListResponse> GetInvites(Guid poolId, Guid managerUserId)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            EnsureCanManageInvites(poolId, managerUserId);

            return _invites
                .Where(invite => invite.PoolId == poolId)
                .OrderBy(invite => invite.IsRevoked ? 1 : 0)
                .ThenByDescending(invite => invite.CreatedAt)
                .Select(invite => new PoolInviteListResponse(
                    invite.Id,
                    invite.Code,
                    invite.PoolId,
                    invite.CreatedByUserId,
                    invite.CreatedAt,
                    invite.RevokedAt,
                    invite.RevokedByUserId,
                    invite.IsRevoked))
                .ToArray();
        }
    }

    public PoolInviteListResponse RevokeInvite(Guid poolId, Guid inviteId, Guid managerUserId)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            EnsureCanManageInvites(poolId, managerUserId);
            var invite = _invites.SingleOrDefault(candidate => candidate.Id == inviteId && candidate.PoolId == poolId)
                ?? throw new KeyNotFoundException("Invite does not exist.");

            invite.Revoke(managerUserId, DateTimeOffset.UtcNow);
            PersistInviteRevocation(invite);
            return new PoolInviteListResponse(
                invite.Id,
                invite.Code,
                invite.PoolId,
                invite.CreatedByUserId,
                invite.CreatedAt,
                invite.RevokedAt,
                invite.RevokedByUserId,
                invite.IsRevoked);
        }
    }

    public IReadOnlyCollection<PoolJoinRequestResponse> GetJoinRequests(Guid poolId, Guid managerUserId)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            EnsureCanManage(poolId, managerUserId);
        }

        using var db = _dbContextFactory.CreateDbContext();
        return (from request in db.PoolJoinRequests.AsNoTracking()
                where request.PoolId == poolId
                join user in db.Users.AsNoTracking()
                    on request.UserId equals user.Id into userGroup
                from user in userGroup.DefaultIfEmpty()
                orderby request.Status == "Pending" ? 0 : 1, request.RequestedAt descending
                select new PoolJoinRequestResponse(
                    request.Id,
                    request.PoolId,
                    request.UserId,
                    user == null ? "Unknown user" : user.DisplayName,
                    user == null ? request.UserId.ToString() : user.Email,
                    request.Status,
                    request.RequestedAt))
            .ToArray();
    }

    public PoolJoinRequestDecisionResponse ApproveJoinRequest(Guid poolId, Guid requestId, Guid managerUserId)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            EnsureCanManage(poolId, managerUserId);

            using var db = _dbContextFactory.CreateDbContext();
            var request = db.PoolJoinRequests.SingleOrDefault(candidate => candidate.Id == requestId && candidate.PoolId == poolId)
                ?? throw new KeyNotFoundException("Join request does not exist.");

            var member = _members.SingleOrDefault(candidate => candidate.PoolId == poolId && candidate.UserId == request.UserId);
            if (member is null)
            {
                member = new PoolMember(Ids.NewId(), poolId, request.UserId, PoolMemberRole.Member, DateTimeOffset.UtcNow);
                _members.Add(member);

                if (!db.PoolMembers.Any(candidate => candidate.PoolId == poolId && candidate.UserId == request.UserId))
                {
                    db.PoolMembers.Add(ToPersistedMember(member));
                }
            }

            request.Status = "Approved";
            db.SaveChanges();
            return new PoolJoinRequestDecisionResponse(request.Id, request.PoolId, request.UserId, request.Status, member.Id);
        }
    }

    public PoolJoinRequestDecisionResponse DenyJoinRequest(Guid poolId, Guid requestId, Guid managerUserId)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            EnsureCanManage(poolId, managerUserId);

            using var db = _dbContextFactory.CreateDbContext();
            var request = db.PoolJoinRequests.SingleOrDefault(candidate => candidate.Id == requestId && candidate.PoolId == poolId)
                ?? throw new KeyNotFoundException("Join request does not exist.");

            request.Status = "Denied";
            db.SaveChanges();
            return new PoolJoinRequestDecisionResponse(request.Id, request.PoolId, request.UserId, request.Status, null);
        }
    }

    public PoolInviteResponse? GetInvite(string code)
    {
        lock (_gate)
        {
            var invite = FindActiveInvite(code);
            if (invite is null)
            {
                return null;
            }

            var pool = _pools.Single(candidate => candidate.Id == invite.PoolId);
            if (pool.IsHidden)
            {
                return null;
            }

            return new PoolInviteResponse(invite.Code, pool.Id, pool.Name, pool.Profile, pool.StartingBalance);
        }
    }

    public PoolDetailsResponse JoinPool(Guid userId, JoinPoolRequest request)
    {
        lock (_gate)
        {
            var invite = FindActiveInvite(request.InviteCode)
                ?? throw new KeyNotFoundException("Invite code does not exist.");

            var pool = _pools.Single(candidate => candidate.Id == invite.PoolId);
            if (pool.IsHidden)
            {
                throw new KeyNotFoundException("Invite code does not exist.");
            }

            var existing = _members.SingleOrDefault(member => member.PoolId == pool.Id && member.UserId == userId);
            if (existing is not null)
            {
                return ToDetails(pool, existing);
            }

            var member = new PoolMember(Ids.NewId(), pool.Id, userId, PoolMemberRole.Member, DateTimeOffset.UtcNow);
            _members.Add(member);
            PersistMember(member);
            return ToDetails(pool, member);
        }
    }

    public IReadOnlyCollection<PoolMember> GetMembers(Guid poolId)
    {
        lock (_gate)
        {
            return _members.Where(member => member.PoolId == poolId).ToArray();
        }
    }

    public PoolMember? GetMember(Guid poolId, Guid userId)
    {
        lock (_gate)
        {
            if (IsPoolHidden(poolId))
            {
                return null;
            }

            return _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == userId);
        }
    }

    public PoolMember UpdateMemberLeaderboardStatus(
        Guid poolId,
        Guid ownerUserId,
        Guid memberId,
        PoolMemberLeaderboardStatus leaderboardStatus)
    {
        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            var owner = _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == ownerUserId);
            if (owner?.Role is not PoolMemberRole.Owner)
            {
                throw new UnauthorizedAccessException("Only the pool owner can update leaderboard status.");
            }

            var member = _members.SingleOrDefault(candidate => candidate.Id == memberId && candidate.PoolId == poolId)
                ?? throw new KeyNotFoundException("Pool member does not exist.");
            member.SetLeaderboardStatus(leaderboardStatus);
            PersistMemberLeaderboardStatus(member);
            return member;
        }
    }

    public PoolMember AddMemberVipAdjustment(Guid poolId, Guid ownerUserId, Guid memberId, int amount)
    {
        if (amount == 0)
        {
            throw new ArgumentException("Adjustment amount cannot be zero.", nameof(amount));
        }

        lock (_gate)
        {
            if (_pools.All(pool => pool.Id != poolId))
            {
                throw new KeyNotFoundException("Pool does not exist.");
            }

            var owner = _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == ownerUserId);
            if (owner?.Role is not PoolMemberRole.Owner)
            {
                throw new UnauthorizedAccessException("Only the pool owner can adjust member balance.");
            }

            var member = _members.SingleOrDefault(candidate => candidate.Id == memberId && candidate.PoolId == poolId)
                ?? throw new KeyNotFoundException("Pool member does not exist.");
            member.AddVipAdjustment(amount);
            PersistMemberVipAdjustment(member);
            return member;
        }
    }

    public bool IsMember(Guid poolId, Guid userId)
    {
        lock (_gate)
        {
            return !IsPoolHidden(poolId) && _members.Any(member => member.PoolId == poolId && member.UserId == userId);
        }
    }

    public IReadOnlyCollection<Market> GetMarkets(Guid poolId)
    {
        lock (_gate)
        {
            return _markets.Where(market => market.PoolId == poolId).ToArray();
        }
    }

    public int SyncMissingMarketsForTournamentPools(TournamentCatalog catalog)
    {
        var addedMarkets = new List<Market>();

        lock (_gate)
        {
            var configuration = _payoutConfigurations.GetActiveConfiguration();
            foreach (var pool in _pools)
            {
                var tournamentEventIds = catalog.GetEvents(pool.TournamentId)
                    .Select(matchEvent => matchEvent.Id)
                    .ToHashSet();
                if (tournamentEventIds.Count == 0)
                {
                    continue;
                }

                var existingEventIds = _markets
                    .Where(market => market.PoolId == pool.Id)
                    .Select(market => market.EventId)
                    .ToHashSet();

                var missingEventIds = tournamentEventIds
                    .Where(eventId => !existingEventIds.Contains(eventId))
                    .ToArray();

                if (missingEventIds.Length == 0)
                {
                    continue;
                }

                var generatedMarkets = missingEventIds
                    .SelectMany(eventId => GenerateMarkets(pool, eventId, configuration))
                    .ToArray();
                _markets.AddRange(generatedMarkets);
                addedMarkets.AddRange(generatedMarkets);
            }
        }

        if (addedMarkets.Count > 0)
        {
            PersistMarkets(addedMarkets);
        }

        return addedMarkets.Count;
    }

    public IReadOnlyCollection<HandicapLineMarketResponse> GetHandicapLineMarkets(Guid eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var poolNames = db.Pools
            .AsNoTracking()
            .Select(pool => new { pool.Id, pool.Name })
            .ToDictionary(pool => pool.Id, pool => pool.Name);

        lock (_gate)
        {
            return _markets
                .Where(market => market.EventId == eventId && market.Type == MarketType.Handicap)
                .OrderBy(market => market.Period)
                .ThenBy(market => poolNames.GetValueOrDefault(market.PoolId, "Unknown pool"))
                .Select(market => new HandicapLineMarketResponse(
                    market.Id,
                    market.PoolId,
                    poolNames.GetValueOrDefault(market.PoolId, "Unknown pool"),
                    market.EventId,
                    market.Period,
                    market.LineValue,
                    market.PayoutMultiplier,
                    market.Status))
                .ToArray();
        }
    }

    public IReadOnlyCollection<HandicapLineMarketResponse> ConfirmHandicapLine(Guid eventId, ConfirmHandicapLineRequest request)
    {
        if (request.MarketPeriod is not (MarketPeriod.FullTime or MarketPeriod.FirstHalf))
        {
            throw new ArgumentException("Market period is not supported.", nameof(request));
        }

        if (!IsValidQuarterLine(request.LineValue))
        {
            throw new ArgumentException("Handicap line must use 0.25 increments.", nameof(request));
        }

        using var db = _dbContextFactory.CreateDbContext();
        var poolNames = db.Pools
            .AsNoTracking()
            .Select(pool => new { pool.Id, pool.Name })
            .ToDictionary(pool => pool.Id, pool => pool.Name);

        lock (_gate)
        {
            var markets = _markets
                .Where(market => market.EventId == eventId && market.Type == MarketType.Handicap && market.Period == request.MarketPeriod)
                .ToArray();

            if (markets.Length == 0)
            {
                throw new KeyNotFoundException("No handicap markets found for this event and period.");
            }

            foreach (var market in markets)
            {
                market.ConfirmLineValue(request.LineValue);
            }

            PersistMarketLineUpdates(markets);

            return markets
                .OrderBy(market => poolNames.GetValueOrDefault(market.PoolId, "Unknown pool"))
                .Select(market => new HandicapLineMarketResponse(
                    market.Id,
                    market.PoolId,
                    poolNames.GetValueOrDefault(market.PoolId, "Unknown pool"),
                    market.EventId,
                    market.Period,
                    market.LineValue,
                    market.PayoutMultiplier,
                    market.Status))
                .ToArray();
        }
    }

    public Market? GetMarket(Guid marketId)
    {
        lock (_gate)
        {
            return _markets.SingleOrDefault(market => market.Id == marketId);
        }
    }

    private static IEnumerable<Market> GenerateMarkets(Pool pool, Guid eventId, PayoutConfigurationResponse configuration)
    {
        return configuration.Rules
            .Where(rule => rule.Profile == pool.Profile && rule.IsEnabled)
            .Select(rule => CreateMarket(pool.Id, eventId, rule, configuration.Version));
    }

    private static Market CreateMarket(
        Guid poolId,
        Guid eventId,
        PayoutMarketRuleResponse rule,
        int payoutConfigurationVersion) =>
        new(
            Ids.NewId(),
            poolId,
            eventId,
            rule.MarketType,
            rule.Period,
            rule.LineValue,
            rule.PayoutMultiplier,
            payoutConfigurationVersion,
            rule.MarketType == MarketType.Handicap ? MarketStatus.LinePending : MarketStatus.Open);

    private void EnsureCanManage(Guid poolId, Guid userId)
    {
        if (IsPoolHidden(poolId))
        {
            throw new UnauthorizedAccessException("This pool is hidden.");
        }

        var membership = _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == userId);
        if (membership?.Role is not (PoolMemberRole.Owner or PoolMemberRole.Admin))
        {
            throw new UnauthorizedAccessException("Only pool owners and admins can manage this pool.");
        }
    }

    private void EnsureCanManageInvites(Guid poolId, Guid userId)
    {
        if (IsPoolHidden(poolId))
        {
            throw new UnauthorizedAccessException("This pool is hidden.");
        }

        var membership = _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == userId);
        if (membership?.Role is not PoolMemberRole.Owner)
        {
            throw new UnauthorizedAccessException("Only pool owners can manage invite codes.");
        }
    }

    private bool IsPoolHidden(Guid poolId) => _pools.SingleOrDefault(pool => pool.Id == poolId)?.IsHidden == true;

    private PoolInvite? FindActiveInvite(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalizedCode = code.Trim();
        return _invites.SingleOrDefault(invite =>
            !invite.IsRevoked &&
            string.Equals(invite.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
    }

    private PoolSummaryResponse ToSummary(Pool pool, PoolMember member) =>
        new(
            pool.Id,
            member.Id,
            pool.Name,
            pool.TournamentId,
            pool.Profile,
            pool.StartingBalance,
            pool.PredictionsLocked,
            pool.CoverImageUrl,
            pool.AnnouncementTitle,
            pool.DefaultStake,
            pool.MinStake,
            pool.MaxStake,
            pool.MaxTotalStakePerEvent,
            pool.VipEventStakeMultiplierEnabled,
            GetVipLevel(member.VipAdjustmentAmount, pool.StartingBalance),
            GetEffectiveMaxTotalStakePerEvent(pool, member),
            pool.LeaderboardMinEventAverageStakePercent,
            member.Role,
            CountMembers(pool.Id),
            CountInvites(pool.Id));

    private PoolDetailsResponse ToDetails(Pool pool, PoolMember member) =>
        new(
            pool.Id,
            member.Id,
            pool.Name,
            pool.TournamentId,
            pool.Profile,
            pool.StartingBalance,
            pool.PredictionsLocked,
            pool.CoverImageUrl,
            pool.AnnouncementTitle,
            pool.DefaultStake,
            pool.MinStake,
            pool.MaxStake,
            pool.MaxTotalStakePerEvent,
            pool.VipEventStakeMultiplierEnabled,
            GetVipLevel(member.VipAdjustmentAmount, pool.StartingBalance),
            GetEffectiveMaxTotalStakePerEvent(pool, member),
            pool.LeaderboardMinEventAverageStakePercent,
            member.Role,
            CountMembers(pool.Id),
            CountInvites(pool.Id));

    private int CountMembers(Guid poolId) => _members.Count(member => member.PoolId == poolId);

    private int CountInvites(Guid poolId) => _invites.Count(invite => invite.PoolId == poolId && !invite.IsRevoked);

    private static string CreateInviteCode()
    {
        Span<byte> bytes = stackalloc byte[9];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private void LoadPersisted()
    {
        using var db = _dbContextFactory.CreateDbContext();

        _pools.AddRange(db.Pools.AsNoTracking().AsEnumerable().Select(pool =>
        {
            var normalizedStakeSettings = NormalizeStakeSettings(
                pool.StartingBalance,
                pool.DefaultStake,
                pool.MinStake,
                pool.MaxStake,
                pool.MaxTotalStakePerEvent);

            return new Pool(
                pool.Id,
                pool.OwnerUserId,
                pool.Name,
                pool.TournamentId,
                pool.Profile,
                pool.StartingBalance,
                pool.PredictionsLocked,
                pool.CoverImageUrl,
                normalizedStakeSettings.defaultStake,
                normalizedStakeSettings.minStake,
                normalizedStakeSettings.maxStake,
                normalizedStakeSettings.maxTotalStakePerEvent,
                pool.VipEventStakeMultiplierEnabled,
                pool.LeaderboardMinEventAverageStakePercent,
                string.IsNullOrWhiteSpace(pool.AnnouncementTitle) ? "Announcements" : pool.AnnouncementTitle,
                pool.IsHidden);
        }));

        _members.AddRange(db.PoolMembers.AsNoTracking().Select(member => new PoolMember(
            member.Id,
            member.PoolId,
            member.UserId,
            member.Role,
            member.JoinedAt,
            member.LeaderboardStatus,
            member.VipAdjustmentAmount)));

        _invites.AddRange(db.PoolInvites.AsNoTracking().Select(invite => new PoolInvite(
            invite.Id,
            invite.PoolId,
            invite.CreatedByUserId,
            invite.Code,
            invite.CreatedAt,
            invite.RevokedAt,
            invite.RevokedByUserId)));

        _markets.AddRange(db.Markets.AsNoTracking().Select(market => new Market(
            market.Id,
            market.PoolId,
            market.EventId,
            market.Type,
            market.Period,
            market.LineValue,
            market.PayoutMultiplier,
            market.PayoutConfigurationVersion,
            market.Status)));
    }

    private void PersistPoolSlice(Pool pool, PoolMember owner, IReadOnlyCollection<Market> markets)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.Pools.Add(ToPersistedPool(pool));
        db.PoolMembers.Add(ToPersistedMember(owner));
        db.Markets.AddRange(markets.Select(ToPersistedMarket));
        db.SaveChanges();
    }

    private void PersistMarkets(IReadOnlyCollection<Market> markets)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.Markets.AddRange(markets.Select(ToPersistedMarket));
        db.SaveChanges();
    }

    private void PersistPoolUpdate(Pool pool)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persisted = db.Pools.Single(candidate => candidate.Id == pool.Id);
        persisted.Name = pool.Name;
        persisted.StartingBalance = pool.StartingBalance;
        persisted.PredictionsLocked = pool.PredictionsLocked;
        persisted.CoverImageUrl = pool.CoverImageUrl;
        persisted.AnnouncementTitle = pool.AnnouncementTitle;
        persisted.IsHidden = pool.IsHidden;
        persisted.DefaultStake = pool.DefaultStake;
        persisted.MinStake = pool.MinStake;
        persisted.MaxStake = pool.MaxStake;
        persisted.MaxTotalStakePerEvent = pool.MaxTotalStakePerEvent;
        persisted.VipEventStakeMultiplierEnabled = pool.VipEventStakeMultiplierEnabled;
        persisted.LeaderboardMinEventAverageStakePercent = pool.LeaderboardMinEventAverageStakePercent;
        db.SaveChanges();
    }

    private void PersistInvite(PoolInvite invite)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.PoolInvites.Add(new PersistedPoolInvite
        {
            Id = invite.Id,
            PoolId = invite.PoolId,
            CreatedByUserId = invite.CreatedByUserId,
            Code = invite.Code,
            CreatedAt = invite.CreatedAt,
            RevokedAt = invite.RevokedAt,
            RevokedByUserId = invite.RevokedByUserId
        });
        db.SaveChanges();
    }

    private void PersistInviteRevocation(PoolInvite invite)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persisted = db.PoolInvites.Single(candidate => candidate.Id == invite.Id);
        persisted.RevokedAt = invite.RevokedAt;
        persisted.RevokedByUserId = invite.RevokedByUserId;
        db.SaveChanges();
    }

    private void PersistMember(PoolMember member)
    {
        using var db = _dbContextFactory.CreateDbContext();
        if (db.PoolMembers.Any(candidate => candidate.PoolId == member.PoolId && candidate.UserId == member.UserId))
        {
            return;
        }

        db.PoolMembers.Add(ToPersistedMember(member));
        db.SaveChanges();
    }

    private void PersistMemberLeaderboardStatus(PoolMember member)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persistedMember = db.PoolMembers.SingleOrDefault(candidate => candidate.Id == member.Id);
        if (persistedMember is null)
        {
            return;
        }

        persistedMember.LeaderboardStatus = member.LeaderboardStatus;
        db.SaveChanges();
    }

    private void PersistMemberVipAdjustment(PoolMember member)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persistedMember = db.PoolMembers.SingleOrDefault(candidate => candidate.Id == member.Id);
        if (persistedMember is null)
        {
            return;
        }

        persistedMember.VipAdjustmentAmount = member.VipAdjustmentAmount;
        db.SaveChanges();
    }

    private void PersistMarketLineUpdates(IReadOnlyCollection<Market> markets)
    {
        using var db = _dbContextFactory.CreateDbContext();
        foreach (var market in markets)
        {
            var persisted = db.Markets.Single(candidate => candidate.Id == market.Id);
            persisted.LineValue = market.LineValue;
            persisted.Status = market.Status;
        }

        db.SaveChanges();
    }

    private static PersistedPool ToPersistedPool(Pool pool) => new()
    {
        Id = pool.Id,
        OwnerUserId = pool.OwnerUserId,
        Name = pool.Name,
        TournamentId = pool.TournamentId,
        Profile = pool.Profile,
        StartingBalance = pool.StartingBalance,
        PredictionsLocked = pool.PredictionsLocked,
        CoverImageUrl = pool.CoverImageUrl,
        AnnouncementTitle = pool.AnnouncementTitle,
        IsHidden = pool.IsHidden,
        DefaultStake = pool.DefaultStake,
        MinStake = pool.MinStake,
        MaxStake = pool.MaxStake,
        MaxTotalStakePerEvent = pool.MaxTotalStakePerEvent,
        VipEventStakeMultiplierEnabled = pool.VipEventStakeMultiplierEnabled,
        LeaderboardMinEventAverageStakePercent = pool.LeaderboardMinEventAverageStakePercent
    };

    private static int GetVipLevel(int vipAdjustmentAmount, int startingBalance) =>
        startingBalance <= 0 ? 0 : Math.Max(0, vipAdjustmentAmount / startingBalance);

    private static int GetEffectiveMaxTotalStakePerEvent(Pool pool, PoolMember member)
    {
        var vipLevel = GetVipLevel(member.VipAdjustmentAmount, pool.StartingBalance);
        var effectiveCap = pool.VipEventStakeMultiplierEnabled
            ? (long)pool.MaxTotalStakePerEvent + ((long)pool.MaxTotalStakePerEvent * vipLevel / 2)
            : pool.MaxTotalStakePerEvent;
        return effectiveCap > int.MaxValue ? int.MaxValue : (int)effectiveCap;
    }

    private static PersistedPoolMember ToPersistedMember(PoolMember member) => new()
    {
        Id = member.Id,
        PoolId = member.PoolId,
        UserId = member.UserId,
        Role = member.Role,
        LeaderboardStatus = member.LeaderboardStatus,
        VipAdjustmentAmount = member.VipAdjustmentAmount,
        JoinedAt = member.JoinedAt
    };

    private static PersistedMarket ToPersistedMarket(Market market) => new()
    {
        Id = market.Id,
        PoolId = market.PoolId,
        EventId = market.EventId,
        Type = market.Type,
        Period = market.Period,
        LineValue = market.LineValue,
        PayoutMultiplier = market.PayoutMultiplier,
        PayoutConfigurationVersion = market.PayoutConfigurationVersion,
        Status = market.Status
    };

    private static bool IsValidQuarterLine(decimal value) => decimal.Remainder(value * 100m, 25m) == 0m;

    private static void ValidateStakeSettings(int defaultStake, int minStake, int maxStake, int maxTotalStakePerEvent)
    {
        if (minStake <= 0)
        {
            throw new ArgumentException("Minimum stake must be greater than zero.", nameof(minStake));
        }

        if (defaultStake <= 0)
        {
            throw new ArgumentException("Default stake must be greater than zero.", nameof(defaultStake));
        }

        if (maxStake <= 0)
        {
            throw new ArgumentException("Maximum stake must be greater than zero.", nameof(maxStake));
        }

        if (maxTotalStakePerEvent <= 0)
        {
            throw new ArgumentException("Maximum total stake per event must be greater than zero.", nameof(maxTotalStakePerEvent));
        }

        if (minStake > defaultStake)
        {
            throw new ArgumentException("Minimum stake cannot exceed default stake.", nameof(minStake));
        }

        if (defaultStake > maxStake)
        {
            throw new ArgumentException("Default stake cannot exceed maximum stake.", nameof(defaultStake));
        }

    }

    private void EnsureCurrentOneXTwoMarketPayouts()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var markets = db.Markets
            .Where(market => market.Type == MarketType.OneXTwo
                && market.Status == MarketStatus.Open
                && market.PayoutMultiplier != CurrentOneXTwoPayoutMultiplier)
            .ToArray();

        if (markets.Length == 0)
        {
            return;
        }

        foreach (var market in markets)
        {
            market.PayoutMultiplier = CurrentOneXTwoPayoutMultiplier;
        }

        db.SaveChanges();
    }

    private static string? NormalizeOptionalUrl(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"{fieldName} must be a valid HTTP or HTTPS URL.", fieldName);
        }

        return normalized;
    }

    private static string NormalizeAnnouncementTitle(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Announcements" : value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Announcement title must be 200 characters or fewer.", nameof(value));
        }

        return normalized;
    }

    private static (int defaultStake, int minStake, int maxStake, int maxTotalStakePerEvent) CreateDefaultStakeSettings(int startingBalance)
    {
        var defaultStake = Math.Max(1, startingBalance / 10);
        var minStake = Math.Max(1, defaultStake / 2);
        var maxStake = Math.Max(defaultStake, defaultStake * 2);
        var maxTotalStakePerEvent = Math.Max(maxStake, defaultStake * 4);
        return (defaultStake, minStake, maxStake, maxTotalStakePerEvent);
    }

    private static (int defaultStake, int minStake, int maxStake, int maxTotalStakePerEvent) NormalizeStakeSettings(
        int startingBalance,
        int defaultStake,
        int minStake,
        int maxStake,
        int maxTotalStakePerEvent)
    {
        if (defaultStake > 0 && minStake > 0 && maxStake > 0 && maxTotalStakePerEvent > 0)
        {
            return (defaultStake, minStake, maxStake, maxTotalStakePerEvent);
        }

        return CreateDefaultStakeSettings(startingBalance);
    }
}

public sealed record ConfirmHandicapLineRequest(MarketPeriod MarketPeriod, decimal LineValue);

public sealed record HandicapLineMarketResponse(
    Guid Id,
    Guid PoolId,
    string PoolName,
    Guid EventId,
    MarketPeriod MarketPeriod,
    decimal? LineValue,
    decimal PayoutMultiplier,
    MarketStatus Status);

public sealed record PoolSummaryResponse(
    Guid Id,
    Guid MemberId,
    string Name,
    Guid TournamentId,
    MarketProfile Profile,
    int StartingBalance,
    bool PredictionsLocked,
    string? CoverImageUrl,
    string AnnouncementTitle,
    int DefaultStake,
    int MinStake,
    int MaxStake,
    int MaxTotalStakePerEvent,
    bool VipEventStakeMultiplierEnabled,
    int MemberVipLevel,
    int EffectiveMaxTotalStakePerEvent,
    decimal LeaderboardMinEventAverageStakePercent,
    PoolMemberRole Role,
    int MemberCount,
    int InviteCount);

public sealed record PoolDetailsResponse(
    Guid Id,
    Guid MemberId,
    string Name,
    Guid TournamentId,
    MarketProfile Profile,
    int StartingBalance,
    bool PredictionsLocked,
    string? CoverImageUrl,
    string AnnouncementTitle,
    int DefaultStake,
    int MinStake,
    int MaxStake,
    int MaxTotalStakePerEvent,
    bool VipEventStakeMultiplierEnabled,
    int MemberVipLevel,
    int EffectiveMaxTotalStakePerEvent,
    decimal LeaderboardMinEventAverageStakePercent,
    PoolMemberRole Role,
    int MemberCount,
    int InviteCount);

public sealed record PoolInviteResponse(
    string Code,
    Guid PoolId,
    string PoolName,
    MarketProfile Profile,
    int StartingBalance);

public sealed record PoolInviteListResponse(
    Guid Id,
    string Code,
    Guid PoolId,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    Guid? RevokedByUserId,
    bool IsRevoked);

public sealed record PoolJoinRequestResponse(
    Guid Id,
    Guid PoolId,
    Guid UserId,
    string DisplayName,
    string Email,
    string Status,
    DateTimeOffset RequestedAt);

public sealed record PoolJoinRequestDecisionResponse(
    Guid Id,
    Guid PoolId,
    Guid UserId,
    string Status,
    Guid? MemberId);

public sealed record UpdateMemberLeaderboardStatusRequest(
    PoolMemberLeaderboardStatus LeaderboardStatus);

public sealed record PoolMemberLeaderboardStatusResponse(
    Guid MemberId,
    PoolMemberLeaderboardStatus LeaderboardStatus);

public sealed record AddMemberBalanceAdjustmentRequest(int Amount);

public sealed record MemberBalanceAdjustmentResponse(
    Guid MemberId,
    int VipAdjustmentAmount,
    int Amount,
    int CurrentBalance);
