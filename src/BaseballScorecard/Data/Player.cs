using System.ComponentModel.DataAnnotations;

namespace BaseballScorecard.Data;

/// <summary>
/// One player on one roster. UniformNumber is text of 1–2 digits, so "0" and "00" differ;
/// the (RosterId, UniformNumber) pair is unique, but the same number can repeat across rosters.
/// </summary>
public class Player
{
    public int Id { get; set; }

    public int RosterId { get; set; }

    [MaxLength(2)]
    public string UniformNumber { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";
}
