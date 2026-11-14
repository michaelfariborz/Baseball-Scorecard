namespace BaseballScorecard.Data;

/// <summary>
/// A player in a team's game, starter or substitute, taking a new fielding position from an inning. The starting
/// <see cref="LineupSlot"/> and the <see cref="Substitution"/> rows stay as saved; the player's positions are his
/// first one, then these rows in inning order. One change per player per inning, so (LineupId, PlayerId, Inning) is unique.
/// </summary>
public class PositionChange
{
    public int Id { get; set; }

    public int LineupId { get; set; }

    public int PlayerId { get; set; }

    /// <summary>A fielding position or DH: never PH or PR.</summary>
    public Position Position { get; set; }

    /// <summary>The first inning the player plays this position: 1 to the game's Innings.</summary>
    public int Inning { get; set; }
}
