using BaseballScorecard.Data;

namespace BaseballScorecard.Tests.Lineups;

// Pins the scorer's position numbers: they are written on every scorecard, so they must never shift.
public class PositionTests
{
    [Fact]
    public void Positions_are_numbered_one_to_twelve_with_their_labels()
    {
        Assert.Equal(
            [
                "1 · Pitcher", "2 · Catcher", "3 · First Base", "4 · Second Base", "5 · Third Base",
                "6 · Shortstop", "7 · Left Field", "8 · Center Field", "9 · Right Field", "10 · Designated Hitter",
                "11 · Pinch Runner", "12 · Pinch Hitter",
            ],
            Positions.All.Select(Positions.Label));
        Assert.Equal(Enumerable.Range(1, 12), Positions.All.Select(p => (int)p));
        Assert.Equal(1, (int)Position.Pitcher);
        Assert.Equal(10, (int)Position.DesignatedHitter);
    }

    [Theory]
    [InlineData(Position.Pitcher, "P")]
    [InlineData(Position.Catcher, "C")]
    [InlineData(Position.FirstBase, "1B")]
    [InlineData(Position.SecondBase, "2B")]
    [InlineData(Position.ThirdBase, "3B")]
    [InlineData(Position.Shortstop, "SS")]
    [InlineData(Position.LeftField, "LF")]
    [InlineData(Position.CenterField, "CF")]
    [InlineData(Position.RightField, "RF")]
    [InlineData(Position.DesignatedHitter, "DH")]
    [InlineData(Position.PinchRunner, "PR")]
    [InlineData(Position.PinchHitter, "PH")]
    public void Each_position_has_its_scorecard_abbreviation(Position position, string abbreviation)
    {
        Assert.Equal(abbreviation, Positions.Abbreviation(position));
        Assert.Equal(12, Positions.All.Count);
        Assert.Equal(11, (int)Position.PinchRunner);
        Assert.Equal(12, (int)Position.PinchHitter);
    }
}
