namespace BaseballScorecard.Data;

/// <summary>
/// A bench player who took over a batting slot of a saved <see cref="Lineup"/> during the game. The starting
/// <see cref="LineupSlot"/> stays as saved; the slot's players are its starter, then these rows in the order they
/// took the slot. A player enters a game once, so the (LineupId, PlayerId) pair is unique.
/// </summary>
public class Substitution
{
    public int Id { get; set; }

    public int LineupId { get; set; }

    /// <summary>1–9.</summary>
    public int BattingOrder { get; set; }

    public int PlayerId { get; set; }

    public SubstitutionKind Kind { get; set; }

    /// <summary>The inning the player came in: 1 to the game's Innings.</summary>
    public int Inning { get; set; }
}

/// <summary>How a substitute came in. Saved as the number, so never renumber.</summary>
public enum SubstitutionKind
{
    PinchHitter = 1,
    PinchRunner = 2,
    Defensive = 3,
}
