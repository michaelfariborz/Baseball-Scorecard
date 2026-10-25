using BaseballScorecard.Data;

namespace BaseballScorecard.Tests.Lineups;

// Pins the scorer's position numbers: they are written on every scorecard, so they must never shift.
public class PositionTests
{
    [Fact]
    public void Positions_are_numbered_one_to_ten_with_their_labels()
    {
        Assert.Equal(
            [
                "1 · Pitcher", "2 · Catcher", "3 · First Base", "4 · Second Base", "5 · Third Base",
                "6 · Shortstop", "7 · Left Field", "8 · Center Field", "9 · Right Field", "10 · Designated Hitter",
            ],
            Positions.All.Select(Positions.Label));
        Assert.Equal(Enumerable.Range(1, 10), Positions.All.Select(p => (int)p));
        Assert.Equal(1, (int)Position.Pitcher);
        Assert.Equal(10, (int)Position.DesignatedHitter);
    }
}
