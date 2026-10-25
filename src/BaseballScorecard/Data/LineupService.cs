using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Loads and saves a team's starting lineup for a game. <see cref="SaveAsync"/> returns a validation message,
/// or null on success; a rejected save changes nothing.
/// </summary>
public class LineupService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const int SlotCount = 9;
    public const string StartingPitcherRequiredMessage =
        "Pick a Starting pitcher from this roster: the lineup has a Designated Hitter.";
    public const string StartingPitcherBattingMessage =
        "The Starting pitcher can't also be in the batting order when the lineup has a Designated Hitter.";
    public const string TeamNotInGameMessage = "This team is not playing in this game.";
    public const string SavedElsewhereMessage =
        "Another save of this lineup finished first. Reload the page to see it, then save again.";

    public static string EmptySlotMessage(int slot) => $"Batting slot {slot} needs a player and a position.";

    public static string SamePlayerMessage(int slot, int earlier) =>
        $"Batting slot {slot} has the same player as slot {earlier}.";

    public static string SamePositionMessage(int slot, int earlier) =>
        $"Batting slot {slot} has the same position as slot {earlier}.";

    public static string PitcherWithDhMessage(int slot) =>
        $"Batting slot {slot} can't be Pitcher when the lineup has a Designated Hitter.";

    public static string NotOnRosterMessage(int slot) => $"Batting slot {slot} has a player who is not on this roster.";

    /// <summary>The saved lineup with its slots in batting order, or null when none is saved yet.</summary>
    public async Task<Lineup?> FindAsync(int gameId, int rosterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Lineups.AsNoTracking()
            .Include(l => l.Slots.OrderBy(s => s.BattingOrder))
            .FirstOrDefaultAsync(l => l.GameId == gameId && l.RosterId == rosterId);
    }

    /// <summary>
    /// Validates and saves the lineup, replacing any saved one for the same game and team.
    /// Slots are read in list order as batting order 1–9.
    /// </summary>
    public async Task<string?> SaveAsync(Lineup lineup)
    {
        var slots = lineup.Slots;
        for (var i = 0; i < SlotCount; i++)
        {
            if (i >= slots.Count || slots[i].PlayerId == 0 || slots[i].Position == 0)
            {
                return EmptySlotMessage(i + 1);
            }
            for (var j = 0; j < i; j++)
            {
                if (slots[j].PlayerId == slots[i].PlayerId)
                {
                    return SamePlayerMessage(i + 1, j + 1);
                }
                if (slots[j].Position == slots[i].Position)
                {
                    return SamePositionMessage(i + 1, j + 1);
                }
            }
        }

        var hasDh = slots.Any(s => s.Position == Position.DesignatedHitter);
        if (hasDh)
        {
            var pitcherSlot = slots.FindIndex(s => s.Position == Position.Pitcher);
            if (pitcherSlot >= 0)
            {
                return PitcherWithDhMessage(pitcherSlot + 1);
            }
            if (lineup.StartingPitcherId is null or 0)
            {
                return StartingPitcherRequiredMessage;
            }
            if (slots.Any(s => s.PlayerId == lineup.StartingPitcherId))
            {
                return StartingPitcherBattingMessage;
            }
        }
        var startingPitcherId = hasDh ? lineup.StartingPitcherId : null;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Games.AnyAsync(g =>
                g.Id == lineup.GameId && (g.AwayRosterId == lineup.RosterId || g.HomeRosterId == lineup.RosterId)))
        {
            return TeamNotInGameMessage;
        }
        var rosterPlayerIds = await db.Players
            .Where(p => p.RosterId == lineup.RosterId)
            .Select(p => p.Id)
            .ToListAsync();
        var offRoster = slots.FindIndex(s => !rosterPlayerIds.Contains(s.PlayerId));
        if (offRoster >= 0)
        {
            return NotOnRosterMessage(offRoster + 1);
        }
        if (startingPitcherId is not null && !rosterPlayerIds.Contains(startingPitcherId.Value))
        {
            return StartingPitcherRequiredMessage;
        }

        var entity = await db.Lineups.Include(l => l.Slots)
            .FirstOrDefaultAsync(l => l.GameId == lineup.GameId && l.RosterId == lineup.RosterId);
        if (entity is null)
        {
            entity = db.Lineups.Add(new Lineup { GameId = lineup.GameId, RosterId = lineup.RosterId }).Entity;
        }
        entity.StartingPitcherId = startingPitcherId;
        // Update the 9 rows in place, so the unique (LineupId, BattingOrder) index never sees two slot 1s.
        for (var i = 0; i < SlotCount; i++)
        {
            var row = entity.Slots.FirstOrDefault(s => s.BattingOrder == i + 1);
            if (row is null)
            {
                row = new LineupSlot { BattingOrder = i + 1 };
                entity.Slots.Add(row);
            }
            row.PlayerId = slots[i].PlayerId;
            row.Position = slots[i].Position;
        }
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another first save of this lineup; the unique index caught it.
            return SavedElsewhereMessage;
        }

        lineup.Id = entity.Id;
        lineup.StartingPitcherId = startingPitcherId;
        return null;
    }
}
