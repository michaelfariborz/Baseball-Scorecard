namespace BaseballScorecard.Data;

/// <summary>One result the scorer can pick for an at bat. <see cref="Code"/> is what is saved in the cell's <see cref="BattingNote"/>.</summary>
public sealed record AtBatResult(string Code, string Label)
{
    /// <summary>Every result the at-bat dialog offers, in the order it lists them.</summary>
    public static IReadOnlyList<AtBatResult> All { get; } =
    [
        new("1B", "single"),
        new("2B", "double"),
        new("3B", "triple"),
        new("HR", "home run"),
        new("BB", "walk"),
        new("HBP", "hit by pitch"),
        new("K", "strikeout swinging"),
        new("ꓘ", "strikeout looking"),
        new("GO", "ground out"),
        new("FO", "fly out"),
        new("SF", "sacrifice fly"),
        new("FC", "fielder's choice"),
        new("E", "reached on error"),
    ];

    /// <summary>The result a saved note holds, or null when the note is blank or older free text.</summary>
    public static AtBatResult? Find(string note) => All.FirstOrDefault(r => r.Code == note);
}
