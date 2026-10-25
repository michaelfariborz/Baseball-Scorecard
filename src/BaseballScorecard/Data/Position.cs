namespace BaseballScorecard.Data;

/// <summary>
/// The scorer's position numbers. 0 (no value) means "not picked yet". Saved as the number, so never renumber.
/// </summary>
public enum Position
{
    Pitcher = 1,
    Catcher = 2,
    FirstBase = 3,
    SecondBase = 4,
    ThirdBase = 5,
    Shortstop = 6,
    LeftField = 7,
    CenterField = 8,
    RightField = 9,
    DesignatedHitter = 10,
}

public static class Positions
{
    /// <summary>Every position, in number order 1–10.</summary>
    public static IReadOnlyList<Position> All { get; } = Enum.GetValues<Position>();

    /// <summary>"1 · Pitcher" … "10 · Designated Hitter".</summary>
    public static string Label(Position position) => $"{(int)position} · {Name(position)}";

    private static string Name(Position position) => position switch
    {
        Position.FirstBase => "First Base",
        Position.SecondBase => "Second Base",
        Position.ThirdBase => "Third Base",
        Position.LeftField => "Left Field",
        Position.CenterField => "Center Field",
        Position.RightField => "Right Field",
        Position.DesignatedHitter => "Designated Hitter",
        _ => position.ToString(),
    };
}
