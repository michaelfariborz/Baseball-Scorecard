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
