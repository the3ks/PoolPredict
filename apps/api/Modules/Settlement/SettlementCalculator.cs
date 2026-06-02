using System.Text.RegularExpressions;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Tournaments;

namespace PoolPredict.Api.Modules.Settlement;

public sealed class SettlementCalculator
{
    private static readonly Regex CorrectScorePattern = new(@"^\d+\-\d+$", RegexOptions.Compiled);

    public SettlementCalculationResult Calculate(SettlementCalculationInput input)
    {
        if (input.Stake <= 0)
        {
            throw new ArgumentException("Stake must be positive.");
        }

        if (input.PayoutMultiplier <= 0)
        {
            throw new ArgumentException("Payout multiplier must be positive.");
        }

        var outcome = Evaluate(input);
        var expectedCredit = outcome switch
        {
            SettlementOutcome.Win => RoundPoints(input.Stake * input.PayoutMultiplier),
            SettlementOutcome.HalfWin => RoundPoints((input.Stake / 2m * input.PayoutMultiplier) + (input.Stake / 2m)),
            SettlementOutcome.Push => input.Stake,
            SettlementOutcome.HalfLose => RoundPoints(input.Stake / 2m),
            SettlementOutcome.Cancelled => input.Stake,
            _ => 0
        };

        return new SettlementCalculationResult(outcome, expectedCredit);
    }

    private static SettlementOutcome Evaluate(SettlementCalculationInput input)
    {
        if (input.EventStatus == EventStatus.Cancelled)
        {
            return SettlementOutcome.Cancelled;
        }

        var homeScore = input.Period == MarketPeriod.FirstHalf
            ? input.FirstHalfHomeScore ?? input.FullTimeHomeScore
            : input.FullTimeHomeScore;
        var awayScore = input.Period == MarketPeriod.FirstHalf
            ? input.FirstHalfAwayScore ?? input.FullTimeAwayScore
            : input.FullTimeAwayScore;

        return input.MarketType switch
        {
            MarketType.Winner => EvaluateWinner(input.SelectedOption, input.HomeParticipant, input.AwayParticipant, homeScore, awayScore),
            MarketType.Handicap => EvaluateHandicap(input.SelectedOption, input.LineValue ?? 0m, input.HomeParticipant, input.AwayParticipant, homeScore, awayScore),
            MarketType.OverUnder => EvaluateOverUnder(input.SelectedOption, input.LineValue ?? throw new ArgumentException("Over/Under markets require a line value."), homeScore + awayScore),
            MarketType.OddEven => EvaluateOddEven(input.SelectedOption, homeScore + awayScore),
            MarketType.CorrectScore => EvaluateCorrectScore(input.SelectedOption, homeScore, awayScore),
            _ => throw new ArgumentException("Unsupported market type.")
        };
    }

    private static SettlementOutcome EvaluateWinner(string selectedOption, string homeParticipant, string awayParticipant, int homeScore, int awayScore)
    {
        ValidateOption(selectedOption, homeParticipant, "Draw", awayParticipant);

        if (homeScore == awayScore)
        {
            return MatchOption(selectedOption, "Draw");
        }

        return homeScore > awayScore
            ? MatchOption(selectedOption, homeParticipant)
            : MatchOption(selectedOption, awayParticipant);
    }

    private static SettlementOutcome EvaluateHandicap(
        string selectedOption,
        decimal line,
        string homeParticipant,
        string awayParticipant,
        int homeScore,
        int awayScore)
    {
        var homeOption = $"{homeParticipant} {FormatLine(line)}";
        var awayOption = $"{awayParticipant} {FormatLine(-line)}";
        ValidateOption(selectedOption, homeOption, awayOption);

        var pickedHome = string.Equals(selectedOption, homeOption, StringComparison.OrdinalIgnoreCase);
        var pickedScore = pickedHome ? homeScore : awayScore;
        var opponentScore = pickedHome ? awayScore : homeScore;
        var effectiveLine = pickedHome ? line : -line;

        if (IsQuarterLine(effectiveLine))
        {
            var split = SplitQuarterLine(effectiveLine);
            var first = EvaluateHandicapLine(pickedScore, opponentScore, split.First);
            var second = EvaluateHandicapLine(pickedScore, opponentScore, split.Second);
            return CombineSplitOutcomes(first, second);
        }

        return EvaluateHandicapLine(pickedScore, opponentScore, effectiveLine);
    }

    private static SettlementOutcome EvaluateOverUnder(string selectedOption, decimal line, int totalScore)
    {
        ValidateOption(selectedOption, $"Over {FormatNumber(line)}", $"Under {FormatNumber(line)}");

        if (totalScore == line)
        {
            return SettlementOutcome.Push;
        }

        if (selectedOption.StartsWith("Over ", StringComparison.OrdinalIgnoreCase))
        {
            return totalScore > line ? SettlementOutcome.Win : SettlementOutcome.Lose;
        }

        return totalScore < line ? SettlementOutcome.Win : SettlementOutcome.Lose;
    }

    private static SettlementOutcome EvaluateOddEven(string selectedOption, int totalScore)
    {
        ValidateOption(selectedOption, "Odd", "Even");

        return totalScore % 2 == 0
            ? MatchOption(selectedOption, "Even")
            : MatchOption(selectedOption, "Odd");
    }

    private static SettlementOutcome EvaluateCorrectScore(string selectedOption, int homeScore, int awayScore)
    {
        var normalized = selectedOption.Replace(" ", "", StringComparison.Ordinal);
        if (!CorrectScorePattern.IsMatch(normalized))
        {
            throw new ArgumentException("Correct Score selections must use the format home-away, for example 2-1.");
        }

        return string.Equals(normalized, $"{homeScore}-{awayScore}", StringComparison.OrdinalIgnoreCase)
            ? SettlementOutcome.Win
            : SettlementOutcome.Lose;
    }

    private static SettlementOutcome EvaluateHandicapLine(int pickedScore, int opponentScore, decimal line)
    {
        var adjustedScore = pickedScore + line;
        if (adjustedScore == opponentScore)
        {
            return SettlementOutcome.Push;
        }

        return adjustedScore > opponentScore ? SettlementOutcome.Win : SettlementOutcome.Lose;
    }

    private static (decimal First, decimal Second) SplitQuarterLine(decimal line)
    {
        var lower = Math.Floor(line * 2m) / 2m;
        var upper = Math.Ceiling(line * 2m) / 2m;
        return (lower, upper);
    }

    private static SettlementOutcome CombineSplitOutcomes(SettlementOutcome first, SettlementOutcome second)
    {
        if (first == second)
        {
            return first;
        }

        if ((first == SettlementOutcome.Win && second == SettlementOutcome.Push)
            || (first == SettlementOutcome.Push && second == SettlementOutcome.Win))
        {
            return SettlementOutcome.HalfWin;
        }

        if ((first == SettlementOutcome.Lose && second == SettlementOutcome.Push)
            || (first == SettlementOutcome.Push && second == SettlementOutcome.Lose))
        {
            return SettlementOutcome.HalfLose;
        }

        throw new InvalidOperationException("Unsupported split handicap outcome.");
    }

    private static bool IsQuarterLine(decimal line)
    {
        var fractional = Math.Abs(line % 1m);
        return fractional == 0.25m || fractional == 0.75m;
    }

    private static SettlementOutcome MatchOption(string selectedOption, string expected) =>
        string.Equals(selectedOption, expected, StringComparison.OrdinalIgnoreCase)
            ? SettlementOutcome.Win
            : SettlementOutcome.Lose;

    private static void ValidateOption(string selectedOption, params string[] validOptions)
    {
        if (!validOptions.Any(option => string.Equals(selectedOption, option, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Invalid selection '{selectedOption}'.");
        }
    }

    private static int RoundPoints(decimal points) =>
        (int)Math.Round(points, MidpointRounding.AwayFromZero);

    private static string FormatLine(decimal value) =>
        value > 0 ? $"+{FormatNumber(value)}" : FormatNumber(value);

    private static string FormatNumber(decimal value) =>
        value.ToString("0.##");
}

public sealed record SettlementCalculationInput(
    MarketType MarketType,
    MarketPeriod Period,
    string SelectedOption,
    int Stake,
    decimal? LineValue,
    decimal PayoutMultiplier,
    string HomeParticipant,
    string AwayParticipant,
    EventStatus EventStatus,
    int FullTimeHomeScore,
    int FullTimeAwayScore,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore);

public sealed record SettlementCalculationResult(SettlementOutcome Outcome, int ExpectedCredit);

public enum SettlementOutcome
{
    Win,
    HalfWin,
    Lose,
    HalfLose,
    Push,
    Cancelled
}
