using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Loads and saves the batting grid: a note per batting slot per inning, and HITS and LOB per team per inning.
/// Each save returns a validation message, or null on success; a rejected save changes nothing.
/// </summary>
public partial class BattingGridService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const int NoteMaxLength = 20;
    public const string NoteTooLongMessage = "A note can be up to 20 characters.";
    public const int MaxBasesReached = 4;
    public const string BasesReachedMessage = "Bases reached must be none, 1st, 2nd, 3rd or home.";
    public const int FieldersMaxLength = 20;
    public const string FieldersMessage =
        "Fielders must be position numbers 1–9, like 8, 63 or 4-6-3 (hyphens are optional, up to 20 characters once shown with hyphens).";
    public const string ErrorFielderMessage = "An error is charged to one fielder: a position number 1–9.";
    public const string MultiOutMessage = "Double or triple play must be none, DP or TP.";
    public const string ErrorMultiOutMessage = "An error is never a DP or TP.";
    public const string NoFieldersMessage = "Only GO, FO, SF, FC and E record fielders.";
    public const string CaughtStealingNotOnBaseMessage =
        "A caught stealing needs the batter on base: pick 1st, 2nd or 3rd for bases reached.";
    public const string CaughtStealingScoredMessage = "A runner who scored cannot be caught stealing.";
    public const string CaughtStealingBaseMessage =
        "Caught stealing must be the next base after bases reached: 2nd after 1st, 3rd after 2nd, home after 3rd.";
    public const string CaughtStealingFieldersMessage =
        "Caught stealing fielders must be position numbers 1–9, like 26 or 2-6.";
    public const string StolenBaseRangeMessage = "Stolen bases must be 2nd, 3rd or home.";
    public const string StolenBaseOnHitMessage =
        "The batter reached that base on the hit itself, so it was not stolen.";
    public const string StolenBaseCaughtMessage =
        "A stolen base must come before the caught-stealing base.";
    public const string StolenBaseNotReachedMessage =
        "A stolen base must be at or below bases reached: the runner cannot steal a base he never got to.";
    public const string WildPitchPassedBallRangeMessage = "Wild pitch and passed ball bases must be 1st, 2nd, 3rd or home.";
    public const string WildPitchPassedBallFirstMessage =
        "Only a strikeout (K or ꓘ) can reach 1st on a wild pitch or passed ball.";
    public const string WildPitchPassedBallOnHitMessage =
        "The batter reached that base on the hit itself, not on a wild pitch or passed ball.";
    public const string WildPitchPassedBallCaughtMessage =
        "A wild pitch or passed ball base must come before the caught-stealing base.";
    public const string WildPitchPassedBallNotReachedMessage =
        "A wild pitch or passed ball base must be at or below bases reached: the runner never got there.";
    public const string WildPitchPassedBallTwiceMessage =
        "A base is taken one way only: stolen, on a wild pitch or on a passed ball.";
    public const string HitsLobMessage = "HITS and LOB must be whole numbers of 0 or more.";
    public const string SlotMessage = "That batting slot is not part of the lineup.";
    public const string InningMessage = "That inning is not part of this game.";
    public const string ColumnMessage = "An at bat goes in column 1 of its inning or an extra column after it.";
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

    /// <summary>
    /// Saves the note, bases reached (0–4), fielders, DP / TP and caught stealing for one slot's inning, text trimmed.
    /// Fielders go only with a result that records them (<see cref="AtBatResult.Fielders"/>); leaving them out
    /// saves them blank. A caught stealing (target base 2–4, 0 none) needs the batter on base short of home and is
    /// at the next base; its fielders are saved only with a target base. Stolen bases (2–4) are saved as a sorted set,
    /// each past the base the play reached, short of the caught-stealing base and at or below bases reached.
    /// Wild pitch and passed ball bases follow the same rules, except 1st is allowed after a strikeout, and no base
    /// may be in more than one of the stolen, wild pitch and passed ball sets.
    /// Blank (or null) note clears the cell, everything else included. <paramref name="column"/> is the inning's column
    /// (1 the first, 2 or more an extra one), each saved apart.
    /// </summary>
    public async Task<string?> SaveNoteAsync(int gameId, int rosterId, int battingOrder, int inning, string? noteText,
        int basesReached = 0, string? fieldersText = null, string? multiOutText = null,
        int caughtStealingBase = 0, string? caughtStealingFieldersText = null, IEnumerable<int>? stolenBases = null, int column = 1,
        IEnumerable<int>? wildPitchBases = null, IEnumerable<int>? passedBallBases = null)
    {
        var note = noteText?.Trim() ?? "";
        if (note.Length > NoteMaxLength)
        {
            return NoteTooLongMessage;
        }
        var fielders = fieldersText?.Trim() ?? "";
        var multiOut = multiOutText?.Trim() ?? "";
        if (note != "" && AtBatResult.Find(note)?.Fielders == FielderEntry.Sequence)
        {
            fielders = HyphenateFielders(fielders);
        }
        if (note != "" && CheckFielders(note, fielders, multiOut) is { } fieldersError)
        {
            return fieldersError;
        }
        if (basesReached < 0 || basesReached > MaxBasesReached)
        {
            return BasesReachedMessage;
        }
        var csFielders = caughtStealingBase == 0 ? "" : HyphenateFielders(caughtStealingFieldersText?.Trim() ?? "");
        if (note != "" && caughtStealingBase != 0 && CheckCaughtStealing(basesReached, caughtStealingBase, csFielders) is { } csError)
        {
            return csError;
        }
        int[] steals = [.. (stolenBases ?? []).Distinct().Order()];
        if (note != "" && CheckStolenBases(note, basesReached, caughtStealingBase, steals) is { } sbError)
        {
            return sbError;
        }
        int[] wildPitches = [.. (wildPitchBases ?? []).Distinct().Order()];
        int[] passedBalls = [.. (passedBallBases ?? []).Distinct().Order()];
        if (note != "" && CheckWildPitchesAndPassedBalls(note, basesReached, caughtStealingBase, steals, wildPitches, passedBalls) is { } wpError)
        {
            return wpError;
        }
        if (battingOrder < 1 || battingOrder > LineupService.SlotCount)
        {
            return SlotMessage;
        }
        if (column < 1)
        {
            return ColumnMessage;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await CheckCellAsync(db, gameId, rosterId, inning) is { } error)
        {
            return error;
        }
        var row = await db.BattingNotes.FirstOrDefaultAsync(n =>
            n.GameId == gameId && n.RosterId == rosterId && n.BattingOrder == battingOrder && n.Inning == inning && n.Column == column);
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
                GameId = gameId, RosterId = rosterId, BattingOrder = battingOrder, Inning = inning, Column = column,
            }).Entity;
            row.Note = note;
            row.BasesReached = basesReached;
            row.Fielders = fielders;
            row.MultiOut = multiOut;
            row.CaughtStealingBase = caughtStealingBase;
            row.CaughtStealingFielders = csFielders;
            row.StolenBases = steals;
            row.WildPitchBases = wildPitches;
            row.PassedBallBases = passedBalls;
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

    /// <summary>The message when the fielders or DP / TP do not fit the result; null when they do (or are blank).</summary>
    private static string? CheckFielders(string note, string fielders, string multiOut)
    {
        switch (AtBatResult.Find(note)?.Fielders ?? FielderEntry.None)
        {
            case FielderEntry.Sequence:
                if (fielders != "" && (fielders.Length > FieldersMaxLength || !FielderSequence().IsMatch(fielders)))
                {
                    return FieldersMessage;
                }
                return multiOut is "" or "DP" or "TP" ? null : MultiOutMessage;
            case FielderEntry.Single:
                if (multiOut != "")
                {
                    return ErrorMultiOutMessage;
                }
                return fielders == "" || OneFielder().IsMatch(fielders) ? null : ErrorFielderMessage;
            default:
                return fielders == "" && multiOut == "" ? null : NoFieldersMessage;
        }
    }

    /// <summary>The message when a caught stealing does not fit the bases reached or its fielders; null when it fits.</summary>
    private static string? CheckCaughtStealing(int basesReached, int target, string fielders)
    {
        if (basesReached == 0)
        {
            return CaughtStealingNotOnBaseMessage;
        }
        if (basesReached == MaxBasesReached)
        {
            return CaughtStealingScoredMessage;
        }
        if (target != basesReached + 1)
        {
            return CaughtStealingBaseMessage;
        }
        return fielders.Length <= FieldersMaxLength && FielderSequence().IsMatch(fielders) ? null : CaughtStealingFieldersMessage;
    }

    /// <summary>The message when a stolen base does not fit the play, the caught stealing or bases reached; null when all fit.</summary>
    private static string? CheckStolenBases(string note, int basesReached, int caughtStealingBase, int[] steals)
    {
        if (steals.Any(b => b < 2 || b > MaxBasesReached))
        {
            return StolenBaseRangeMessage;
        }
        if (steals.Any(b => b <= AtBatResult.BaseOnPlay(note)))
        {
            return StolenBaseOnHitMessage;
        }
        if (caughtStealingBase != 0 && steals.Any(b => b >= caughtStealingBase))
        {
            return StolenBaseCaughtMessage;
        }
        return steals.Any(b => b > basesReached) ? StolenBaseNotReachedMessage : null;
    }

    /// <summary>
    /// The message when a wild pitch or passed ball base does not fit the play, the caught stealing, bases reached or
    /// the other marks; null when all fit. 1st needs a strikeout; the base the play itself reached never counts.
    /// </summary>
    private static string? CheckWildPitchesAndPassedBalls(string note, int basesReached, int caughtStealingBase,
        int[] steals, int[] wildPitches, int[] passedBalls)
    {
        int[] marks = [.. wildPitches, .. passedBalls];
        if (marks.Any(b => b < 1 || b > MaxBasesReached))
        {
            return WildPitchPassedBallRangeMessage;
        }
        if (marks.Contains(1) && !AtBatResult.IsStrikeout(note))
        {
            return WildPitchPassedBallFirstMessage;
        }
        if (marks.Any(b => b > 1 && b <= AtBatResult.BaseOnPlay(note)))
        {
            return WildPitchPassedBallOnHitMessage;
        }
        if (caughtStealingBase != 0 && marks.Any(b => b >= caughtStealingBase))
        {
            return WildPitchPassedBallCaughtMessage;
        }
        if (marks.Any(b => b > basesReached))
        {
            return WildPitchPassedBallNotReachedMessage;
        }
        return wildPitches.Intersect(passedBalls).Any() || steals.Intersect(marks).Any() ? WildPitchPassedBallTwiceMessage : null;
    }

    /// <summary>"463" and "46-3" become "4-6-3". Anything that is not digits 1–9 in hyphen-separated groups is left as typed, for the check to refuse.</summary>
    private static string HyphenateFielders(string fielders) =>
        FielderDigits().IsMatch(fielders) ? string.Join('-', fielders.Replace("-", "").ToCharArray()) : fielders;

    [GeneratedRegex(@"^[1-9]+(-[1-9]+)*$")]
    private static partial Regex FielderDigits();

    [GeneratedRegex(@"^[1-9](-[1-9])*$")]
    private static partial Regex FielderSequence();

    [GeneratedRegex(@"^[1-9]$")]
    private static partial Regex OneFielder();

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
