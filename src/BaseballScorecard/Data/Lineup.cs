namespace BaseballScorecard.Data;

/// <summary>
/// One team's starting lineup for one game: 9 batting slots, plus a starting pitcher who does not bat
/// when the lineup has a Designated Hitter (null otherwise). The (GameId, RosterId) pair is unique.
/// </summary>
public class Lineup
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int RosterId { get; set; }

    public int? StartingPitcherId { get; set; }

    /// <summary>In batting order: Slots[0] bats first.</summary>
    public List<LineupSlot> Slots { get; set; } = [];
}

/// <summary>One batting slot. 0 in PlayerId or Position means "not picked yet".</summary>
public class LineupSlot
{
    public int Id { get; set; }

    public int LineupId { get; set; }

    /// <summary>1–9.</summary>
    public int BattingOrder { get; set; }

    public int PlayerId { get; set; }

    public Position Position { get; set; }
}
