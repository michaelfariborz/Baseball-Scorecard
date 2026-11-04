using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Lists, adds and removes the pitchers of a game, and saves their decisions and pitching lines.
/// Each change returns a validation message, or null on success; a refused change changes nothing.
/// </summary>
public class PitchingService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string TeamNotInGameMessage = "This team is not playing in this game.";
    public const string NotOnRosterMessage = "That player is not on this roster.";
    public const string AlreadyPitchingMessage = "That pitcher is already in the table.";
    public const string DecisionMessage = "Decision must be blank, W, L, H or S.";
    public const string InningsPitchedMessage = "IP must be whole innings, or .1 or .2 for outs.";
    public const string CountMessage = "Pitching stats must be whole numbers of 0 or more.";
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

    /// <summary>
    /// Saves one box of a pitcher row's line. Blank (or null) empties the box. IP takes whole innings,
    /// or .1 or .2 for extra outs; every other box takes digits only (so no sign, decimal or text).
    /// </summary>
    public async Task<string?> SetStatAsync(int gameId, int pitcherId, PitchingStat stat, string? text)
    {
        var trimmed = text?.Trim() ?? "";
        int? value = null;
        if (trimmed != "")
        {
            var parsed = stat == PitchingStat.InningsPitched ? ParseOuts(trimmed) : ParseCount(trimmed);
            if (parsed is null)
            {
                return stat == PitchingStat.InningsPitched ? InningsPitchedMessage : CountMessage;
            }
            value = parsed;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var updated = await db.GamePitchers.Where(p => p.Id == pitcherId && p.GameId == gameId)
            .ExecuteUpdateAsync(s =>
            {
                switch (stat)
                {
                    case PitchingStat.InningsPitched: s.SetProperty(p => p.InningsPitchedOuts, value); break;
                    case PitchingStat.BattersFaced: s.SetProperty(p => p.BattersFaced, value); break;
                    case PitchingStat.Walks: s.SetProperty(p => p.Walks, value); break;
                    case PitchingStat.Strikeouts: s.SetProperty(p => p.Strikeouts, value); break;
                    case PitchingStat.Hits: s.SetProperty(p => p.Hits, value); break;
                    case PitchingStat.Runs: s.SetProperty(p => p.Runs, value); break;
                    case PitchingStat.EarnedRuns: s.SetProperty(p => p.EarnedRuns, value); break;
                    default: throw new ArgumentOutOfRangeException(nameof(stat));
                }
            });
        return updated == 0 ? PitcherRemovedMessage : null;
    }

    private static int? ParseCount(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    // "6" is 18 outs, "6.1" is 19, "6.2" is 20; anything else (6.3, a sign, "6.") is null.
    private static int? ParseOuts(string text)
    {
        var parts = text.Split('.');
        if (parts.Length > 2 || ParseCount(parts[0]) is not { } innings)
        {
            return null;
        }
        var extra = 0;
        if (parts.Length == 2)
        {
            if (parts[1] is not ("1" or "2"))
            {
                return null;
            }
            extra = parts[1][0] - '0';
        }
        var outs = 3L * innings + extra;
        return outs <= int.MaxValue ? (int)outs : null;
    }
}
