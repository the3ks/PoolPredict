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

        lock (_gate)
        {
            var pool = new Pool(Ids.NewId(), ownerUserId, request.Name.Trim(), request.TournamentId, request.Profile, request.StartingBalance);
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
                    _pools,
                    member => member.PoolId,
                    pool => pool.Id,
                    (member, pool) => ToSummary(pool, member.Role))
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
            return pool is null || membership is null
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

        lock (_gate)
        {
            var pool = _pools.SingleOrDefault(candidate => candidate.Id == poolId)
                ?? throw new KeyNotFoundException("Pool does not exist.");

            EnsureCanManage(poolId, userId);
            pool.UpdateSettings(request.Name.Trim(), request.StartingBalance);
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

            EnsureCanManage(poolId, userId);

            var invite = new PoolInvite(Ids.NewId(), poolId, userId, CreateInviteCode(), DateTimeOffset.UtcNow);
            _invites.Add(invite);
            PersistInvite(invite);
            return invite;
        }
    }

    public PoolInviteResponse? GetInvite(string code)
    {
        lock (_gate)
        {
            var invite = FindInvite(code);
            if (invite is null)
            {
                return null;
            }

            var pool = _pools.Single(candidate => candidate.Id == invite.PoolId);
            return new PoolInviteResponse(invite.Code, pool.Id, pool.Name, pool.Profile, pool.StartingBalance);
        }
    }

    public PoolDetailsResponse JoinPool(Guid userId, JoinPoolRequest request)
    {
        lock (_gate)
        {
            var invite = FindInvite(request.InviteCode)
                ?? throw new KeyNotFoundException("Invite code does not exist.");

            var pool = _pools.Single(candidate => candidate.Id == invite.PoolId);
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
            return _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == userId);
        }
    }

    public bool IsMember(Guid poolId, Guid userId)
    {
        lock (_gate)
        {
            return _members.Any(member => member.PoolId == poolId && member.UserId == userId);
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
            payoutConfigurationVersion);

    private void EnsureCanManage(Guid poolId, Guid userId)
    {
        var membership = _members.SingleOrDefault(member => member.PoolId == poolId && member.UserId == userId);
        if (membership?.Role is not (PoolMemberRole.Owner or PoolMemberRole.Admin))
        {
            throw new UnauthorizedAccessException("Only pool owners and admins can manage this pool.");
        }
    }

    private PoolInvite? FindInvite(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalizedCode = code.Trim();
        return _invites.SingleOrDefault(invite => string.Equals(invite.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
    }

    private PoolSummaryResponse ToSummary(Pool pool, PoolMemberRole role) =>
        new(pool.Id, pool.Name, pool.TournamentId, pool.Profile, pool.StartingBalance, role, CountMembers(pool.Id), CountInvites(pool.Id));

    private PoolDetailsResponse ToDetails(Pool pool, PoolMember member) =>
        new(pool.Id, member.Id, pool.Name, pool.TournamentId, pool.Profile, pool.StartingBalance, member.Role, CountMembers(pool.Id), CountInvites(pool.Id));

    private int CountMembers(Guid poolId) => _members.Count(member => member.PoolId == poolId);

    private int CountInvites(Guid poolId) => _invites.Count(invite => invite.PoolId == poolId);

    private static string CreateInviteCode()
    {
        Span<byte> bytes = stackalloc byte[9];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private void LoadPersisted()
    {
        using var db = _dbContextFactory.CreateDbContext();

        _pools.AddRange(db.Pools.AsNoTracking().Select(pool => new Pool(
            pool.Id,
            pool.OwnerUserId,
            pool.Name,
            pool.TournamentId,
            pool.Profile,
            pool.StartingBalance)));

        _members.AddRange(db.PoolMembers.AsNoTracking().Select(member => new PoolMember(
            member.Id,
            member.PoolId,
            member.UserId,
            member.Role,
            member.JoinedAt)));

        _invites.AddRange(db.PoolInvites.AsNoTracking().Select(invite => new PoolInvite(
            invite.Id,
            invite.PoolId,
            invite.CreatedByUserId,
            invite.Code,
            invite.CreatedAt)));

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

    private void PersistPoolUpdate(Pool pool)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persisted = db.Pools.Single(candidate => candidate.Id == pool.Id);
        persisted.Name = pool.Name;
        persisted.StartingBalance = pool.StartingBalance;
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
            CreatedAt = invite.CreatedAt
        });
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

    private static PersistedPool ToPersistedPool(Pool pool) => new()
    {
        Id = pool.Id,
        OwnerUserId = pool.OwnerUserId,
        Name = pool.Name,
        TournamentId = pool.TournamentId,
        Profile = pool.Profile,
        StartingBalance = pool.StartingBalance
    };

    private static PersistedPoolMember ToPersistedMember(PoolMember member) => new()
    {
        Id = member.Id,
        PoolId = member.PoolId,
        UserId = member.UserId,
        Role = member.Role,
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
}

public sealed record PoolSummaryResponse(
    Guid Id,
    string Name,
    Guid TournamentId,
    MarketProfile Profile,
    int StartingBalance,
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
    PoolMemberRole Role,
    int MemberCount,
    int InviteCount);

public sealed record PoolInviteResponse(
    string Code,
    Guid PoolId,
    string PoolName,
    MarketProfile Profile,
    int StartingBalance);
