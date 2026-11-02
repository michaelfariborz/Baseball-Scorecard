namespace BaseballScorecard.Data;

/// <summary>
/// One team's HITS and LOB for one inning, as the scorer typed them (null = blank). A row with both blank is removed.
/// The (GameId, RosterId, Inning) triple is unique.
/// </summary>
public class InningHitsLob
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int RosterId { get; set; }

    /// <summary>1 to the game's Innings.</summary>
    public int Inning { get; set; }

    /// <summary>0 or more.</summary>
    public int? Hits { get; set; }

    /// <summary>Runners left on base; 0 or more.</summary>
    public int? Lob { get; set; }
}
