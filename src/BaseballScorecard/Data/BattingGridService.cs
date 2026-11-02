using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Loads and saves the batting grid: a note per batting slot per inning, and HITS and LOB per team per inning.
/// Each save returns a validation message, or null on success; a rejected save changes nothing.
/// </summary>
public class BattingGridService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const int NoteMaxLength = 20;
    public const string NoteTooLongMessage = "A note can be up to 20 characters.";
    public const string HitsLobMessage = "HITS and LOB must be whole numbers of 0 or more.";
    public const string SlotMessage = "That batting slot is not part of the lineup.";
    public const string InningMessage = "That inning is not part of this game.";
    public const string TeamNotInGameMessage = "This team is not playing in this game.";
    public const string SavedElsewhereMessage =
        "Another save of this cell finished first. Reload the page to see it, then save again.";

    /// <summary>Every saved note of the game, both teams.</summary>
    public async Task<List<BattingNote>> ListNotesAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BattingNotes.AsNoTracking().Where(n => n.GameId == gameId).ToListAsync();
    }

    /// <summary>Every saved HITS / LOB inning of the game, both teams.</summary>
    public async Task<List<InningHitsLob>> ListHitsLobAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.InningHitsLob.AsNoTracking().Where(t => t.GameId == gameId).ToListAsync();
    }

    /// <summary>Saves the note for one slot's inning, trimmed. Blank (or null) clears the cell.</summary>
    public async Task<string?> SaveNoteAsync(int gameId, int rosterId, int battingOrder, int inning, string? noteText)
    {
        var note = noteText?.Trim() ?? "";
        if (note.Length > NoteMaxLength)
        {
            return NoteTooLongMessage;
        }
        if (battingOrder < 1 || battingOrder > LineupService.SlotCount)
        {
            return SlotMessage;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await CheckCellAsync(db, gameId, rosterId, inning) is { } error)
        {
            return error;
        }
        var row = await db.BattingNotes.FirstOrDefaultAsync(n =>
            n.GameId == gameId && n.RosterId == rosterId && n.BattingOrder == battingOrder && n.Inning == inning);
        if (note == "")
        {
            if (row is not null)
            {
                db.BattingNotes.Remove(row);
            }
        }
        else
        {
            row ??= db.BattingNotes.Add(new BattingNote
            {
                GameId = gameId, RosterId = rosterId, BattingOrder = battingOrder, Inning = inning,
            }).Entity;
            row.Note = note;
        }
        return await SaveAsync(db);
    }

    /// <summary>Saves the team's HITS for one inning. Blank clears it; anything but digits is rejected.</summary>
    public Task<string?> SaveHitsAsync(int gameId, int rosterId, int inning, string? hitsText) =>
        SaveCountAsync(gameId, rosterId, inning, hitsText, (row, n) => row.Hits = n);

    /// <summary>Saves the team's LOB for one inning. Blank clears it; anything but digits is rejected.</summary>
    public Task<string?> SaveLobAsync(int gameId, int rosterId, int inning, string? lobText) =>
        SaveCountAsync(gameId, rosterId, inning, lobText, (row, n) => row.Lob = n);

    private async Task<string?> SaveCountAsync(int gameId, int rosterId, int inning, string? text,
        Action<InningHitsLob, int?> set)
    {
        var trimmed = text?.Trim() ?? "";
        int? count = null;
        if (trimmed != "")
        {
            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return HitsLobMessage;
            }
            count = parsed;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await CheckCellAsync(db, gameId, rosterId, inning) is { } error)
        {
            return error;
        }
        var row = await db.InningHitsLob.FirstOrDefaultAsync(t =>
            t.GameId == gameId && t.RosterId == rosterId && t.Inning == inning);
        if (row is null)
        {
            if (count is null)
            {
                return null;
            }
            row = db.InningHitsLob.Add(new InningHitsLob { GameId = gameId, RosterId = rosterId, Inning = inning }).Entity;
        }
        set(row, count);
        if (row.Hits is null && row.Lob is null)
        {
            db.InningHitsLob.Remove(row);
        }
        return await SaveAsync(db);
    }

    /// <summary>The message when the team is not in the game or the inning is outside it; null when the cell exists.</summary>
    private static async Task<string?> CheckCellAsync(ScorecardDbContext db, int gameId, int rosterId, int inning)
    {
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null || (game.AwayRosterId != rosterId && game.HomeRosterId != rosterId))
        {
            return TeamNotInGameMessage;
        }
        return inning < 1 || inning > game.Innings ? InningMessage : null;
    }

    private static async Task<string?> SaveAsync(ScorecardDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another first save of this cell; the unique index caught it.
            return SavedElsewhereMessage;
        }
        return null;
    }
}
