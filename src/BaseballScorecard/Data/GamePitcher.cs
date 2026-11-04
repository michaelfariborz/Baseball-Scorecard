namespace BaseballScorecard.Data;

/// <summary>
/// One pitcher who appeared for one team in one game, with the scorer's typed-in decision.
/// Rows list in the order they were added (by Id). The (GameId, PlayerId) pair is unique.
/// </summary>
public class GamePitcher
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int RosterId { get; set; }

    public int PlayerId { get; set; }

    public PitcherDecision Decision { get; set; }

    // The pitching line, typed in by the scorer (none is computed from the grid). Null is an empty box.

    /// <summary>Innings pitched, stored as outs: IP 6.1 is 19.</summary>
    public int? InningsPitchedOuts { get; set; }

    public int? BattersFaced { get; set; }

    public int? Walks { get; set; }

    public int? Strikeouts { get; set; }

    public int? Hits { get; set; }

    public int? Runs { get; set; }

    public int? EarnedRuns { get; set; }
}

/// <summary>One box of the pitching line, in scorecard order.</summary>
public enum PitchingStat
{
    InningsPitched,
    BattersFaced,
    Walks,
    Strikeouts,
    Hits,
    Runs,
    EarnedRuns,
}

public static class PitchingStats
{
    /// <summary>Every box, in scorecard order: IP, BF, BB, SO, H, R, ER.</summary>
    public static IReadOnlyList<PitchingStat> All { get; } = Enum.GetValues<PitchingStat>();

    /// <summary>The scorecard column heading.</summary>
    public static string Label(PitchingStat stat) => stat switch
    {
        PitchingStat.InningsPitched => "IP",
        PitchingStat.BattersFaced => "BF",
        PitchingStat.Walks => "BB",
        PitchingStat.Strikeouts => "SO",
        PitchingStat.Hits => "H",
        PitchingStat.Runs => "R",
        PitchingStat.EarnedRuns => "ER",
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };

    /// <summary>The stored value: outs for IP, the count for the rest; null when empty.</summary>
    public static int? Value(GamePitcher p, PitchingStat stat) => stat switch
    {
        PitchingStat.InningsPitched => p.InningsPitchedOuts,
        PitchingStat.BattersFaced => p.BattersFaced,
        PitchingStat.Walks => p.Walks,
        PitchingStat.Strikeouts => p.Strikeouts,
        PitchingStat.Hits => p.Hits,
        PitchingStat.Runs => p.Runs,
        PitchingStat.EarnedRuns => p.EarnedRuns,
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };

    /// <summary>What the box shows: "" when empty; IP as innings plus .1 or .2 for extra outs (19 outs is "6.1").</summary>
    public static string Text(GamePitcher p, PitchingStat stat) => Value(p, stat) switch
    {
        null => "",
        int outs when stat == PitchingStat.InningsPitched => outs % 3 == 0 ? $"{outs / 3}" : $"{outs / 3}.{outs % 3}",
        int n => $"{n}",
    };
}

/// <summary>A pitcher's decision. Saved as the number, so never renumber.</summary>
public enum PitcherDecision
{
    None = 0,
    Win = 1,
    Loss = 2,
    Hold = 3,
    Save = 4,
}

public static class PitcherDecisions
{
    /// <summary>Every decision, blank first.</summary>
    public static IReadOnlyList<PitcherDecision> All { get; } = Enum.GetValues<PitcherDecision>();

    /// <summary>The scorecard letter: blank, W, L, H or S.</summary>
    public static string Label(PitcherDecision decision) => decision == PitcherDecision.None ? "" : decision.ToString()[..1];
}
