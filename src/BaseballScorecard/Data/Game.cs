namespace BaseballScorecard.Data;

/// <summary>
/// One game between two rosters of the same Year. Batters is always 9: positions are unique per lineup.
/// </summary>
public class Game
{
    public const int FixedBatters = 9;

    public int Id { get; set; }

    public int Year { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>0 means no team picked yet.</summary>
    public int AwayRosterId { get; set; }

    public Roster? AwayRoster { get; set; }

    /// <summary>0 means no team picked yet.</summary>
    public int HomeRosterId { get; set; }

    public Roster? HomeRoster { get; set; }

    public int Innings { get; set; } = 9;

    public int Batters { get; set; } = FixedBatters;
}
