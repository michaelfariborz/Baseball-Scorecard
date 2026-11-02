using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Loads and saves the runs each team scored per inning. <see cref="SaveAsync"/> returns a validation message,
/// or null on success; a rejected save changes nothing.
/// </summary>
public class LineScoreService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string RunsMessage = "Runs must be a whole number of 0 or more.";
    public const string InningMessage = "That inning is not part of this game.";
    public const string TeamNotInGameMessage = "This team is not playing in this game.";
    public const string SavedElsewhereMessage =
        "Another save of this inning finished first. Reload the page to see it, then save again.";

    /// <summary>Every saved inning of the game, both teams.</summary>
    public async Task<List<InningRuns>> ListAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.InningRuns.AsNoTracking()
            .Where(r => r.GameId == gameId)
            .OrderBy(r => r.RosterId).ThenBy(r => r.Inning)
            .ToListAsync();
    }

    /// <summary>
    /// Saves the typed runs for one team's inning. Blank (or null) clears the inning; anything but
    /// digits (so no sign, decimal or text) is rejected.
    /// </summary>
    public async Task<string?> SaveAsync(int gameId, int rosterId, int inning, string? runsText)
    {
        var text = runsText?.Trim() ?? "";
        int? runs = null;
        if (text != "")
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return RunsMessage;
            }
            runs = parsed;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null || (game.AwayRosterId != rosterId && game.HomeRosterId != rosterId))
        {
            return TeamNotInGameMessage;
        }
        if (inning < 1 || inning > game.Innings)
        {
            return InningMessage;
        }

        var row = await db.InningRuns
            .FirstOrDefaultAsync(r => r.GameId == gameId && r.RosterId == rosterId && r.Inning == inning);
        if (runs is null)
        {
            if (row is not null)
            {
                db.InningRuns.Remove(row);
            }
        }
        else
        {
            row ??= db.InningRuns.Add(new InningRuns { GameId = gameId, RosterId = rosterId, Inning = inning }).Entity;
            row.Runs = runs.Value;
        }
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another first save of this inning; the unique index caught it.
            return SavedElsewhereMessage;
        }
        return null;
    }
}
