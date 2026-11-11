using System.ComponentModel.DataAnnotations;

namespace BaseballScorecard.Data;

/// <summary>
/// The scorer's free-text note (<c>1B</c>, <c>K</c>, <c>6-3</c>) for one batting-order slot in one inning.
/// Keyed by slot, not player, so a note stays with its slot when the lineup changes. A blank cell has no row.
/// The (GameId, RosterId, BattingOrder, Inning) quadruple is unique.
/// </summary>
public class BattingNote
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int RosterId { get; set; }

    /// <summary>1–9.</summary>
    public int BattingOrder { get; set; }

    /// <summary>1 to the game's Innings.</summary>
    public int Inning { get; set; }

    [MaxLength(BattingGridService.NoteMaxLength)]
    public string Note { get; set; } = "";

    /// <summary>The furthest base the batter reached: 0 none, 1 first, 2 second, 3 third, 4 home.</summary>
    public int BasesReached { get; set; }
}
