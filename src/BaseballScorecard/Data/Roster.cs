using System.ComponentModel.DataAnnotations;

namespace BaseballScorecard.Data;

/// <summary>
/// One team in one season. "2026 Cubs" and "2025 Cubs" are separate rosters; the (TeamName, Year) pair is unique.
/// </summary>
public class Roster
{
    public int Id { get; set; }

    [Required(ErrorMessage = RosterService.BlankNameMessage)]
    [MaxLength(100)]
    public string TeamName { get; set; } = "";

    public int Year { get; set; }
}
