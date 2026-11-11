namespace BaseballScorecard.Data;

/// <summary>Which fielders an at-bat result records.</summary>
public enum FielderEntry
{
    /// <summary>No fielders (hits, walks, strikeouts).</summary>
    None,
    /// <summary>The fielding sequence (<c>6-3</c>), optionally a DP or TP.</summary>
    Sequence,
    /// <summary>The one fielder charged with an error.</summary>
    Single,
}

/// <summary>One result the scorer can pick for an at bat. <see cref="Code"/> is what is saved in the cell's <see cref="BattingNote"/>.</summary>
public sealed record AtBatResult(string Code, string Label, FielderEntry Fielders = FielderEntry.None)
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
        new("GO", "ground out", FielderEntry.Sequence),
        new("FO", "fly out", FielderEntry.Sequence),
        new("SF", "sacrifice fly", FielderEntry.Sequence),
        new("FC", "fielder's choice", FielderEntry.Sequence),
        new("E", "reached on error", FielderEntry.Single),
    ];

    /// <summary>The result a saved note holds, or null when the note is blank or older free text.</summary>
    public static AtBatResult? Find(string note) => All.FirstOrDefault(r => r.Code == note);

    /// <summary>What the grid cell shows: <c>GO 4-6-3 DP</c>, <c>FO 8</c>, <c>E6</c>, or the note alone.</summary>
    public static string CellText(BattingNote note) =>
        Find(note.Note)?.Fielders == FielderEntry.Single
            ? note.Note + note.Fielders
            : string.Join(' ', new[] { note.Note, note.Fielders, note.MultiOut }.Where(s => s != ""));
}
