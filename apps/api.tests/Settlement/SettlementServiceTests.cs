using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Points;
using PoolPredict.Api.Domain.Tournaments;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Predictions;
using PoolPredict.Api.Modules.Settlement;

namespace PoolPredict.Api.Tests.Settlement;

public sealed class SettlementServiceTests
{
    [Fact]
    public async Task ReSettlementCreatesCorrectionDeltaWithoutDuplicatePayout()
    {
        var factory = TestDbContextFactory.Create();
        var fixture = await SeedPredictionAsync(factory, MarketType.Handicap, "Home FC +0.5", lineValue: 0.5m);
        var service = new SettlementService(factory, new PredictionStore(factory), new SettlementCalculator());

        var first = await service.RecordResultAndSettleAsync(
            fixture.EventId,
            new SetEventResultRequest(2, 1, null, null));
        var second = await service.RecordResultAndSettleAsync(
            fixture.EventId,
            new SetEventResultRequest(1, 2, null, null));

        await using var db = await factory.CreateDbContextAsync();
        var settlementEntries = await db.PointLedger
            .Where(entry => entry.PredictionId == fixture.PredictionId
                && (entry.Reason == PointLedgerReason.SettlementPayout
                    || entry.Reason == PointLedgerReason.SettlementRefund
                    || entry.Reason == PointLedgerReason.AdminCorrection))
            .OrderBy(entry => entry.CreatedAt)
            .ToArrayAsync();

        Assert.Equal(1, first.LedgerEntriesCreated);
        Assert.Equal(1, second.LedgerEntriesCreated);
        Assert.Equal(2, settlementEntries.Length);
        Assert.Equal(200, settlementEntries[0].Points);
        Assert.Equal(PointLedgerReason.SettlementPayout, settlementEntries[0].Reason);
        Assert.Equal(-200, settlementEntries[1].Points);
        Assert.Equal(PointLedgerReason.AdminCorrection, settlementEntries[1].Reason);
    }

    [Fact]
    public async Task ReSettlementWithoutResultChangeDoesNotCreateDuplicateLedgerEntry()
    {
        var factory = TestDbContextFactory.Create();
        var fixture = await SeedPredictionAsync(factory, MarketType.Handicap, "Home FC +0.5", lineValue: 0.5m);
        var service = new SettlementService(factory, new PredictionStore(factory), new SettlementCalculator());

        await service.RecordResultAndSettleAsync(fixture.EventId, new SetEventResultRequest(2, 1, null, null));
        var rerun = await service.RecordResultAndSettleAsync(fixture.EventId, new SetEventResultRequest(2, 1, null, null));

        await using var db = await factory.CreateDbContextAsync();
        var settlementEntryCount = await db.PointLedger.CountAsync(entry => entry.PredictionId == fixture.PredictionId
            && (entry.Reason == PointLedgerReason.SettlementPayout
                || entry.Reason == PointLedgerReason.SettlementRefund
                || entry.Reason == PointLedgerReason.AdminCorrection));

        Assert.Equal(1, rerun.UnchangedPredictions);
        Assert.Equal(0, rerun.LedgerEntriesCreated);
        Assert.Equal(1, settlementEntryCount);
    }

    [Fact]
    public async Task CancelledEventSettlementRefundsStakeAndVoidsMarkets()
    {
        var factory = TestDbContextFactory.Create();
        var fixture = await SeedPredictionAsync(factory, MarketType.OverUnder, "Over 2.5", lineValue: 2.5m);
        var service = new SettlementService(factory, new PredictionStore(factory), new SettlementCalculator());

        var response = await service.RecordResultAndSettleAsync(
            fixture.EventId,
            new SetEventResultRequest(0, 0, null, null, IsCancelled: true));

        await using var db = await factory.CreateDbContextAsync();
        var refund = await db.PointLedger.SingleAsync(entry =>
            entry.PredictionId == fixture.PredictionId && entry.Reason == PointLedgerReason.SettlementRefund);
        var market = await db.Markets.SingleAsync(entry => entry.Id == fixture.MarketId);
        var matchEvent = await db.Events.SingleAsync(entry => entry.Id == fixture.EventId);

        Assert.Equal(1, response.LedgerEntriesCreated);
        Assert.Equal(100, refund.Points);
        Assert.Equal(MarketStatus.Voided, market.Status);
        Assert.Equal(EventStatus.Cancelled, matchEvent.Status);
    }

    private static async Task<SettlementFixture> SeedPredictionAsync(
        TestDbContextFactory factory,
        MarketType marketType,
        string selectedOption,
        decimal? lineValue)
    {
        var tournamentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var poolId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var predictionId = Guid.NewGuid();

        await using var db = await factory.CreateDbContextAsync();
        db.Events.Add(new PersistedEvent
        {
            Id = eventId,
            TournamentId = tournamentId,
            HomeParticipantId = Guid.NewGuid(),
            AwayParticipantId = Guid.NewGuid(),
            ExternalId = $"event-{eventId:N}",
            Provider = "Test",
            IsTestData = true,
            ManagementMode = EventManagementMode.Provider,
            HomeParticipant = "Home FC",
            AwayParticipant = "Away FC",
            StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
            Status = EventStatus.Finished
        });
        db.Markets.Add(new PersistedMarket
        {
            Id = marketId,
            PoolId = poolId,
            EventId = eventId,
            Type = marketType,
            Period = MarketPeriod.FullTime,
            LineValue = lineValue,
            PayoutMultiplier = 2m,
            PayoutConfigurationVersion = 1,
            Status = MarketStatus.Locked
        });
        db.Predictions.Add(new PersistedPrediction
        {
            Id = predictionId,
            PoolId = poolId,
            MemberId = memberId,
            MarketId = marketId,
            SelectedOption = selectedOption,
            Stake = 100,
            MarketType = marketType,
            MarketPeriod = MarketPeriod.FullTime,
            LineValueSnapshot = lineValue,
            PayoutMultiplierSnapshot = 2m,
            PayoutConfigurationVersionSnapshot = 1,
            SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        });
        db.PointLedger.Add(new PersistedPointLedgerEntry
        {
            Id = Guid.NewGuid(),
            PoolId = poolId,
            MemberId = memberId,
            Points = -100,
            Reason = PointLedgerReason.PredictionSubmitted,
            PredictionId = predictionId,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        });
        await db.SaveChangesAsync();

        return new SettlementFixture(eventId, marketId, predictionId);
    }

    private sealed record SettlementFixture(Guid EventId, Guid MarketId, Guid PredictionId);

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
