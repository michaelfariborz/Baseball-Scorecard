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

    /// <summary>
    /// Fielders by position number in the order the ball moved (<c>4-6-3</c>), or the one fielder charged with an
    /// error (<c>6</c>). Blank when the result records no fielders.
    /// </summary>
    [MaxLength(BattingGridService.FieldersMaxLength)]
    public string Fielders { get; set; } = "";

    /// <summary><c>DP</c> or <c>TP</c> for a double or triple play; blank otherwise.</summary>
    [MaxLength(2)]
    public string MultiOut { get; set; } = "";

    /// <summary>
    /// The base the runner was going to when caught stealing: 0 none, 2 second, 3 third, 4 home. Always the next base
    /// after <see cref="BasesReached"/>, which stays the base the runner held.
    /// </summary>
    public int CaughtStealingBase { get; set; }

    /// <summary>Fielders on the caught-stealing throw, hyphen-joined (<c>2-6</c>); blank with no caught stealing.</summary>
    [MaxLength(BattingGridService.FieldersMaxLength)]
    public string CaughtStealingFielders { get; set; } = "";
}
