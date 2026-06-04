using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Tournaments;
using PoolPredict.Api.Modules.Settlement;

namespace PoolPredict.Api.Tests.Settlement;

public sealed class SettlementCalculatorTests
{
    private readonly SettlementCalculator _calculator = new();

    [Theory]
    [InlineData(MarketType.OverUnder, "Over 2.5", 2.5, 2, 1, SettlementOutcome.Win, 200)]
    [InlineData(MarketType.OverUnder, "Under 2.5", 2.5, 2, 1, SettlementOutcome.Lose, 0)]
    [InlineData(MarketType.OverUnder, "Under 3", 3.0, 2, 1, SettlementOutcome.Push, 100)]
    [InlineData(MarketType.OneXTwo, "Home FC", null, 2, 1, SettlementOutcome.Win, 200)]
    [InlineData(MarketType.OneXTwo, "Draw", null, 1, 1, SettlementOutcome.Win, 200)]
    [InlineData(MarketType.OneXTwo, "Away FC", null, 2, 1, SettlementOutcome.Lose, 0)]
    [InlineData(MarketType.OddEven, "Odd", null, 2, 1, SettlementOutcome.Win, 200)]
    [InlineData(MarketType.OddEven, "Even", null, 2, 1, SettlementOutcome.Lose, 0)]
    [InlineData(MarketType.CorrectScore, "2-1", null, 2, 1, SettlementOutcome.Win, 500)]
    [InlineData(MarketType.CorrectScore, "1-2", null, 2, 1, SettlementOutcome.Lose, 0)]
    public void CalculatesBasicMarketOutcomes(
        MarketType marketType,
        string selectedOption,
        double? lineValue,
        int homeScore,
        int awayScore,
        SettlementOutcome expectedOutcome,
        int expectedCredit)
    {
        var result = _calculator.Calculate(Input(
            marketType,
            selectedOption,
            lineValue is null ? null : (decimal)lineValue,
            marketType == MarketType.CorrectScore ? 5m : 2m,
            homeScore,
            awayScore));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedCredit, result.ExpectedCredit);
    }

    [Theory]
    [InlineData("Home FC +0.5", 0.5, 1, 1, SettlementOutcome.Win, 200)]
    [InlineData("Home FC 0", 0, 1, 1, SettlementOutcome.Push, 100)]
    [InlineData("Home FC -0.5", -0.5, 1, 1, SettlementOutcome.Lose, 0)]
    [InlineData("Home FC +0.25", 0.25, 1, 1, SettlementOutcome.HalfWin, 150)]
    [InlineData("Home FC -0.25", -0.25, 1, 1, SettlementOutcome.HalfLose, 50)]
    [InlineData("Home FC +0.75", 0.75, 0, 1, SettlementOutcome.HalfLose, 50)]
    [InlineData("Home FC -0.75", -0.75, 2, 1, SettlementOutcome.HalfWin, 150)]
    [InlineData("Away FC -0.25", 0.25, 1, 1, SettlementOutcome.HalfLose, 50)]
    [InlineData("Away FC +0.25", -0.25, 1, 1, SettlementOutcome.HalfWin, 150)]
    public void CalculatesHandicapQuarterLineOutcomes(
        string selectedOption,
        double lineValue,
        int homeScore,
        int awayScore,
        SettlementOutcome expectedOutcome,
        int expectedCredit)
    {
        var result = _calculator.Calculate(Input(
            MarketType.Handicap,
            selectedOption,
            (decimal)lineValue,
            2m,
            homeScore,
            awayScore));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedCredit, result.ExpectedCredit);
    }

    [Fact]
    public void RefundsStakeForCancelledEvent()
    {
        var result = _calculator.Calculate(Input(
            MarketType.OverUnder,
            "Over 2.5",
            2.5m,
            2m,
            0,
            0,
            EventStatus.Cancelled));

        Assert.Equal(SettlementOutcome.Cancelled, result.Outcome);
        Assert.Equal(100, result.ExpectedCredit);
    }

    [Theory]
    [InlineData(MarketType.Handicap, "Home FC", 0.5)]
    [InlineData(MarketType.OneXTwo, "Home or draw", null)]
    [InlineData(MarketType.OverUnder, "Above 2.5", 2.5)]
    [InlineData(MarketType.OddEven, "Neither", null)]
    [InlineData(MarketType.CorrectScore, "2:1", null)]
    public void RejectsInvalidSelections(MarketType marketType, string selectedOption, double? lineValue)
    {
        Assert.Throws<ArgumentException>(() => _calculator.Calculate(Input(
            marketType,
            selectedOption,
            lineValue is null ? null : (decimal)lineValue,
            2m,
            2,
            1)));
    }

    [Fact]
    public void UsesFirstHalfScoresForFirstHalfMarkets()
    {
        var result = _calculator.Calculate(Input(
            MarketType.OddEven,
            "Even",
            null,
            2m,
            3,
            1,
            period: MarketPeriod.FirstHalf,
            firstHalfHomeScore: 1,
            firstHalfAwayScore: 1));

        Assert.Equal(SettlementOutcome.Win, result.Outcome);
        Assert.Equal(200, result.ExpectedCredit);
    }

    private static SettlementCalculationInput Input(
        MarketType marketType,
        string selectedOption,
        decimal? lineValue,
        decimal payoutMultiplier,
        int fullTimeHomeScore,
        int fullTimeAwayScore,
        EventStatus eventStatus = EventStatus.Finished,
        MarketPeriod period = MarketPeriod.FullTime,
        int? firstHalfHomeScore = null,
        int? firstHalfAwayScore = null) =>
        new(
            marketType,
            period,
            selectedOption,
            100,
            lineValue,
            payoutMultiplier,
            "Home FC",
            "Away FC",
            eventStatus,
            fullTimeHomeScore,
            fullTimeAwayScore,
            firstHalfHomeScore,
            firstHalfAwayScore);
}
