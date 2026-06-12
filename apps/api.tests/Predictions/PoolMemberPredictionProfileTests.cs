using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Points;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Domain.Tournaments;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Markets;
using PoolPredict.Api.Modules.Predictions;

namespace PoolPredict.Api.Tests.Predictions;

public sealed class PoolMemberPredictionProfileTests
{
    [Fact]
    public async Task BuildsPoolMemberProfileFromLeaderboardAndPredictionHistory()
    {
        var factory = TestDbContextFactory.Create();
        var poolId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var predictionId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Add(new PersistedUser
            {
                Id = userId,
                Email = "member@example.com",
                NormalizedEmail = "MEMBER@EXAMPLE.COM",
                DisplayName = "Member One",
                Role = Domain.Identity.UserRole.PoolMember,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.PoolMembers.Add(new PersistedPoolMember
            {
                Id = memberId,
                PoolId = poolId,
                UserId = userId,
                Role = PoolMemberRole.Member,
                LeaderboardStatus = PoolMemberLeaderboardStatus.Ranked,
                JoinedAt = DateTimeOffset.UtcNow
            });
            db.Events.Add(new PersistedEvent
            {
                Id = eventId,
                TournamentId = Guid.NewGuid(),
                ExternalId = "event-1",
                HomeParticipant = "Home",
                AwayParticipant = "Away",
                StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = EventStatus.Settled,
                Provider = "Test",
                IsTestData = true
            });
            db.Markets.Add(new PersistedMarket
            {
                Id = marketId,
                PoolId = poolId,
                EventId = eventId,
                Type = MarketType.OneXTwo,
                Period = MarketPeriod.FullTime,
                PayoutMultiplier = 2m,
                PayoutConfigurationVersion = 1,
                Status = MarketStatus.Settled
            });
            db.Predictions.Add(new PersistedPrediction
            {
                Id = predictionId,
                PoolId = poolId,
                MemberId = memberId,
                MarketId = marketId,
                SelectedOption = "Home",
                Stake = 100,
                MarketType = MarketType.OneXTwo,
                MarketPeriod = MarketPeriod.FullTime,
                PayoutMultiplierSnapshot = 2m,
                PayoutConfigurationVersionSnapshot = 1,
                SubmittedAt = DateTimeOffset.UtcNow.AddHours(-2)
            });
            db.PointLedger.AddRange(
                new PersistedPointLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolId,
                    MemberId = memberId,
                    Points = 1000,
                    Reason = PointLedgerReason.StartingBalance,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
                },
                new PersistedPointLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolId,
                    MemberId = memberId,
                    Points = -100,
                    Reason = PointLedgerReason.PredictionSubmitted,
                    PredictionId = predictionId,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
                },
                new PersistedPointLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolId,
                    MemberId = memberId,
                    Points = 200,
                    Reason = PointLedgerReason.SettlementPayout,
                    PredictionId = predictionId,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
                });
            await db.SaveChangesAsync();
        }

        var store = new PredictionStore(factory, Options.Create(new MarketOptions()));

        var profile = store.GetPoolMemberProfile(poolId, memberId, startingBalance: 1000);

        Assert.NotNull(profile);
        Assert.Equal(memberId, profile.MemberId);
        Assert.Equal("Member One", profile.DisplayName);
        Assert.Equal(1, profile.Rank);
        Assert.Equal(1100, profile.Balance);
        Assert.Equal(100, profile.WinLoss);
        Assert.Equal(1, profile.PredictionCount);
        Assert.Equal(1, profile.SettledPredictionCount);
        Assert.Equal(1, profile.WinCount);
        Assert.Equal(100m, profile.WinRate);
        Assert.Single(profile.Predictions);
        Assert.Single(profile.MarketBreakdown);
        Assert.Single(profile.OutcomeBreakdown);
    }

    [Fact]
    public void ReturnsNullForMissingPoolMember()
    {
        var store = new PredictionStore(TestDbContextFactory.Create(), Options.Create(new MarketOptions()));

        Assert.Null(store.GetPoolMemberProfile(Guid.NewGuid(), Guid.NewGuid(), startingBalance: 1000));
    }

    private sealed class TestDbContextFactory(DbContextOptions<PoolPredictDbContext> options)
        : IDbContextFactory<PoolPredictDbContext>
    {
        public PoolPredictDbContext CreateDbContext() => new(options);

        public Task<PoolPredictDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        public static TestDbContextFactory Create()
        {
            var options = new DbContextOptionsBuilder<PoolPredictDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            return new TestDbContextFactory(options);
        }
    }
}
