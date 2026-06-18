using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Points;
using PoolPredict.Api.Domain.Predictions;
using PoolPredict.Api.Domain.Settlement;
using PoolPredict.Api.Domain.Tournaments;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Admin;
using PoolPredict.Api.Modules.Predictions;

namespace PoolPredict.Api.Modules.Settlement;

public sealed class SettlementService(
    IDbContextFactory<PoolPredictDbContext> dbContextFactory,
    PredictionStore predictions,
    SettlementCalculator calculator,
    DatabaseBackupSettingsStore backupSettings,
    DatabaseBackupService backupService,
    ILogger<SettlementService> logger)
{
    public async Task<SettlementResponse> RecordResultAndSettleAsync(
        Guid eventId,
        SetEventResultRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateResult(request);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var matchEvent = await db.Events.SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken)
            ?? throw new KeyNotFoundException("Event does not exist.");

        var persistedResult = await db.EventResults.SingleOrDefaultAsync(result => result.EventId == eventId, cancellationToken);
        if (persistedResult is null)
        {
            persistedResult = new PersistedEventResult
            {
                Id = Ids.NewId(),
                EventId = eventId,
            };
            db.EventResults.Add(persistedResult);
        }

        persistedResult.FullTimeHomeScore = request.FullTimeHomeScore;
        persistedResult.FullTimeAwayScore = request.FullTimeAwayScore;
        persistedResult.FirstHalfHomeScore = request.FirstHalfHomeScore;
        persistedResult.FirstHalfAwayScore = request.FirstHalfAwayScore;
        persistedResult.RecordedAt = DateTimeOffset.UtcNow;
        matchEvent.ManagementMode = EventManagementMode.Manual;
        matchEvent.Status = request.IsCancelled ? EventStatus.Cancelled : EventStatus.Finished;

        var run = new PersistedSettlementRun
        {
            Id = Ids.NewId(),
            EventId = eventId,
            Status = SettlementRunStatus.Started,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.SettlementRuns.Add(run);

        var markets = await db.Markets
            .Where(market => market.EventId == eventId)
            .ToArrayAsync(cancellationToken);
        if (!request.IsCancelled
            && markets.Any(market => market.Period == MarketPeriod.FirstHalf)
            && (request.FirstHalfHomeScore is null || request.FirstHalfAwayScore is null))
        {
            throw new ArgumentException("First-half scores are required when first-half markets exist.");
        }

        var marketIds = markets.Select(market => market.Id).ToList();
        var marketById = markets.ToDictionary(market => market.Id);
        var eventPredictions = await db.Predictions
            .Where(prediction =>
                marketIds.Contains(prediction.MarketId)
                && prediction.Status == PredictionStatus.Active)
            .ToArrayAsync(cancellationToken);
        var existingSettlementCredits = await db.PointLedger
            .Where(entry => entry.PredictionId != null
                && (entry.Reason == PointLedgerReason.SettlementPayout
                    || entry.Reason == PointLedgerReason.SettlementRefund
                    || entry.Reason == PointLedgerReason.AdminCorrection))
            .GroupBy(entry => entry.PredictionId!.Value)
            .Select(group => new { PredictionId = group.Key, Points = group.Sum(entry => entry.Points) })
            .ToArrayAsync(cancellationToken);
        var existingCreditByPredictionId = existingSettlementCredits.ToDictionary(item => item.PredictionId, item => item.Points);

        var unchanged = 0;
        var settled = 0;
        var ledgerEntries = new List<PersistedPointLedgerEntry>();

        foreach (var prediction in eventPredictions)
        {
            var market = marketById[prediction.MarketId];
            var calculation = calculator.Calculate(new SettlementCalculationInput(
                prediction.MarketType,
                market.Period,
                prediction.SelectedOption,
                prediction.Stake,
                prediction.LineValueSnapshot,
                prediction.PayoutMultiplierSnapshot,
                matchEvent.HomeParticipant,
                matchEvent.AwayParticipant,
                matchEvent.Status,
                persistedResult.FullTimeHomeScore,
                persistedResult.FullTimeAwayScore,
                persistedResult.FirstHalfHomeScore,
                persistedResult.FirstHalfAwayScore));
            var expectedCredit = calculation.ExpectedCredit;
            var existingCredit = existingCreditByPredictionId.GetValueOrDefault(prediction.Id);
            var delta = expectedCredit - existingCredit;

            if (delta != 0)
            {
                ledgerEntries.Add(new PersistedPointLedgerEntry
                {
                    Id = Ids.NewId(),
                    PoolId = prediction.PoolId,
                    MemberId = prediction.MemberId,
                    Points = delta,
                    Reason = existingCredit == 0
                        ? calculation.Outcome is SettlementOutcome.Win or SettlementOutcome.HalfWin
                            ? PointLedgerReason.SettlementPayout
                            : PointLedgerReason.SettlementRefund
                        : PointLedgerReason.AdminCorrection,
                    PredictionId = prediction.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                unchanged++;
            }

            db.SettlementLogs.Add(new PersistedSettlementLog
            {
                Id = Ids.NewId(),
                SettlementRunId = run.Id,
                PredictionId = prediction.Id,
                Level = SettlementLogLevel.Info,
                Message = $"Prediction settled as {calculation.Outcome}. ExpectedCredit={expectedCredit}; ExistingCredit={existingCredit}; Delta={delta}.",
                CreatedAt = DateTimeOffset.UtcNow
            });
            settled++;
        }

        foreach (var market in markets)
        {
            market.Status = request.IsCancelled ? MarketStatus.Voided : MarketStatus.Settled;
        }

        matchEvent.Status = request.IsCancelled ? EventStatus.Cancelled : EventStatus.Settled;
        db.PointLedger.AddRange(ledgerEntries);
        run.Status = SettlementRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        predictions.AddPersistedLedgerEntries(ledgerEntries);

        var backupRecipientEmail = backupSettings.GetRecipientEmail();
        var backupAttempted = !string.IsNullOrWhiteSpace(backupRecipientEmail);
        var backupSucceeded = false;
        string? backupMessage;

        if (backupAttempted)
        {
            try
            {
                var backupResult = await backupService.CreateAndSendAsync(backupRecipientEmail!, cancellationToken);
                backupSucceeded = true;
                backupMessage = backupResult.Message;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automatic database backup failed after settling event {EventId}", eventId);
                backupMessage = $"Settlement completed, but automatic database backup failed: {ex.Message}";
            }
        }
        else
        {
            backupMessage = "Settlement completed. Automatic database backup was skipped because no backup recipient email is configured.";
        }

        return new SettlementResponse(
            run.Id,
            eventId,
            settled,
            unchanged,
            ledgerEntries.Count,
            "Completed",
            backupAttempted,
            backupSucceeded,
            backupMessage);
    }

    private static void ValidateResult(SetEventResultRequest request)
    {
        if (request.FullTimeHomeScore < 0 || request.FullTimeAwayScore < 0)
        {
            throw new ArgumentException("Full-time scores cannot be negative.");
        }

        if (request.FirstHalfHomeScore is < 0 || request.FirstHalfAwayScore is < 0)
        {
            throw new ArgumentException("First-half scores cannot be negative.");
        }
    }

}
