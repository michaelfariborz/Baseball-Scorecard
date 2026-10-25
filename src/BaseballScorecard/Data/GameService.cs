using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Data;

/// <summary>
/// Creates and loads games. <see cref="CreateAsync"/> returns a validation message, or null on success.
/// </summary>
public class GameService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string NoTeamMessage = "Pick an Away Team and a Home Team from the selected Year's rosters.";
    public const string SameTeamMessage = "Away Team and Home Team must be different.";
    public const string InningsMessage = "Innings must be a whole number of at least 1.";
    public const string YearMessage = "Enter a Year to pick the teams.";

    public async Task<List<Roster>> RostersForYearAsync(int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Rosters.AsNoTracking()
            .Where(r => r.Year == year)
            .OrderBy(r => r.TeamName)
            .ToListAsync();
    }

    public async Task<Game?> FindAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Games.AsNoTracking()
            .Include(g => g.AwayRoster)
            .Include(g => g.HomeRoster)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <summary>Validates and inserts a new game, then sets its Id. Batters is always saved as 9.</summary>
    public async Task<string?> CreateAsync(Game game)
    {
        if (game.AwayRosterId == 0 || game.HomeRosterId == 0)
        {
            return NoTeamMessage;
        }
        if (game.AwayRosterId == game.HomeRosterId)
        {
            return SameTeamMessage;
        }
        if (game.Innings < 1)
        {
            return InningsMessage;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var teamsInYear = await db.Rosters.CountAsync(r =>
            r.Year == game.Year && (r.Id == game.AwayRosterId || r.Id == game.HomeRosterId));
        if (teamsInYear != 2)
        {
            return NoTeamMessage;
        }

        var entity = new Game
        {
            Year = game.Year,
            Date = game.Date,
            AwayRosterId = game.AwayRosterId,
            HomeRosterId = game.HomeRosterId,
            Innings = game.Innings,
            Batters = Game.FixedBatters,
        };
        db.Games.Add(entity);
        await db.SaveChangesAsync();

        game.Id = entity.Id;
        game.Batters = Game.FixedBatters;
        return null;
    }
}
