using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolPredict.Api.Domain.Points;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Markets;
using PoolPredict.Api.Modules.Predictions;

namespace PoolPredict.Api.Tests.Predictions;

public sealed class PredictionBalanceAdjustmentTests
{
    [Fact]
    public async Task InitializeMemberBalanceCreatesSingleStartingBalanceEntry()
    {
        var factory = TestDbContextFactory.Create();
        var store = CreatePredictionStore(factory);
        var poolId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        store.InitializeMemberBalance(poolId, memberId, 1000);
        store.InitializeMemberBalance(poolId, memberId, 1000);

        await using var db = await factory.CreateDbContextAsync();
        var entries = await db.PointLedger
            .Where(entry => entry.PoolId == poolId && entry.MemberId == memberId)
            .OrderBy(entry => entry.CreatedAt)
            .ToArrayAsync();

        Assert.Single(entries);
        Assert.Equal(PointLedgerReason.StartingBalance, entries[0].Reason);
        Assert.Equal(1000, entries[0].Points);
    }

    [Fact]
    public async Task ApplyStartingBalanceAdjustmentBackfillsMissingMembersAndAddsDelta()
    {
        var factory = TestDbContextFactory.Create();
        var poolId = Guid.NewGuid();
        var initializedMemberId = Guid.NewGuid();
        var legacyMemberId = Guid.NewGuid();
        var initializedUserId = Guid.NewGuid();
        var legacyUserId = Guid.NewGuid();

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.PointLedger.AddRange(
                new PersistedPointLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolId,
                    MemberId = initializedMemberId,
                    Points = 1000,
                    Reason = PointLedgerReason.StartingBalance,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
                },
                new PersistedPointLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolId,
                    MemberId = initializedMemberId,
                    Points = -200,
                    Reason = PointLedgerReason.PredictionSubmitted,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                },
                new PersistedPointLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolId,
                    MemberId = legacyMemberId,
                    Points = -150,
                    Reason = PointLedgerReason.PredictionSubmitted,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4)
                });
            await db.SaveChangesAsync();
        }

        var store = CreatePredictionStore(factory);
        store.ApplyStartingBalanceAdjustment(
            poolId,
            [
                new PoolMember(initializedMemberId, poolId, initializedUserId, PoolMemberRole.Member, DateTimeOffset.UtcNow),
                new PoolMember(legacyMemberId, poolId, legacyUserId, PoolMemberRole.Member, DateTimeOffset.UtcNow)
            ],
            1000,
            2000);

        await using var verifyDb = await factory.CreateDbContextAsync();
        var initializedEntries = await verifyDb.PointLedger
            .Where(entry => entry.PoolId == poolId && entry.MemberId == initializedMemberId)
            .OrderBy(entry => entry.CreatedAt)
            .ToArrayAsync();
        var legacyEntries = await verifyDb.PointLedger
            .Where(entry => entry.PoolId == poolId && entry.MemberId == legacyMemberId)
            .OrderBy(entry => entry.CreatedAt)
            .ToArrayAsync();

        Assert.Equal(3, initializedEntries.Length);
        Assert.Equal(1, initializedEntries.Count(entry => entry.Reason == PointLedgerReason.StartingBalance));
        Assert.Equal(1, initializedEntries.Count(entry => entry.Reason == PointLedgerReason.PredictionSubmitted));
        Assert.Equal(1, initializedEntries.Count(entry => entry.Reason == PointLedgerReason.StartingBalanceAdjustment));
        Assert.Equal(1800, initializedEntries.Sum(entry => entry.Points));

        Assert.Equal(3, legacyEntries.Length);
        Assert.Equal(1, legacyEntries.Count(entry => entry.Reason == PointLedgerReason.PredictionSubmitted));
        Assert.Equal(1, legacyEntries.Count(entry => entry.Reason == PointLedgerReason.StartingBalance));
        Assert.Equal(1, legacyEntries.Count(entry => entry.Reason == PointLedgerReason.StartingBalanceAdjustment));
        Assert.Equal(1850, legacyEntries.Sum(entry => entry.Points));
    }

    private static PredictionStore CreatePredictionStore(TestDbContextFactory factory) =>
        new(factory, Options.Create(new MarketOptions()));

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
