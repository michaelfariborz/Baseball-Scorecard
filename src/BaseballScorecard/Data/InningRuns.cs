namespace BaseballScorecard.Data;

/// <summary>
/// The runs one team scored in one inning of a game, as the scorer typed them. A blank inning has no row.
/// The (GameId, RosterId, Inning) triple is unique.
/// </summary>
public class InningRuns
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int RosterId { get; set; }

    /// <summary>1 to the game's Innings.</summary>
    public int Inning { get; set; }

    /// <summary>0 or more.</summary>
    public int Runs { get; set; }
}
