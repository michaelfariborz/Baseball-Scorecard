using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Lists, adds and removes the pitchers of a game, and saves their decisions.
/// Each change returns a validation message, or null on success; a refused change changes nothing.
/// </summary>
public class PitchingService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string TeamNotInGameMessage = "This team is not playing in this game.";
    public const string NotOnRosterMessage = "That player is not on this roster.";
    public const string AlreadyPitchingMessage = "That pitcher is already in the table.";
    public const string DecisionMessage = "Decision must be blank, W, L, H or S.";
    public const string PitcherRemovedMessage = "This pitcher was removed, so the change was not saved.";

    /// <summary>Every pitcher of the game, both teams, in the order they were added.</summary>
    public async Task<List<GamePitcher>> ListAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GamePitchers.AsNoTracking().Where(p => p.GameId == gameId).OrderBy(p => p.Id).ToListAsync();
    }

    /// <summary>Adds the player to the end of the team's pitchers, with a blank decision.</summary>
    public async Task<string?> AddAsync(int gameId, int rosterId, int playerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Games.AnyAsync(g => g.Id == gameId && (g.AwayRosterId == rosterId || g.HomeRosterId == rosterId)))
        {
            return TeamNotInGameMessage;
        }
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.RosterId == rosterId))
        {
            return NotOnRosterMessage;
        }
        if (await db.GamePitchers.AnyAsync(p => p.GameId == gameId && p.PlayerId == playerId))
        {
            return AlreadyPitchingMessage;
        }

        db.GamePitchers.Add(new GamePitcher { GameId = gameId, RosterId = rosterId, PlayerId = playerId });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another add of the same pitcher; the unique index caught it.
            return AlreadyPitchingMessage;
        }
        return null;
    }

    /// <summary>Removes one pitcher row of the game. A row already gone (or of another game) is left alone.</summary>
    public async Task RemoveAsync(int gameId, int pitcherId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.GamePitchers.Where(p => p.Id == pitcherId && p.GameId == gameId).ExecuteDeleteAsync();
    }

    /// <summary>Saves one pitcher row's decision.</summary>
    public async Task<string?> SetDecisionAsync(int gameId, int pitcherId, PitcherDecision decision)
    {
        if (!Enum.IsDefined(decision))
        {
            return DecisionMessage;
        }
        await using var db = await dbFactory.CreateDbContextAsync();
        var updated = await db.GamePitchers.Where(p => p.Id == pitcherId && p.GameId == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Decision, decision));
        return updated == 0 ? PitcherRemovedMessage : null;
    }
}
