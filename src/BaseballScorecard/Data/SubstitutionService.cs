using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>One player who held a batting slot: the starter (Inning null) or a substitute.</summary>
/// <param name="Position">The Pos column: the starter's position, PH, PR, or for a defensive sub the starter's position.</param>
/// <param name="OwnsFrom">The first inning whose at bat is this player's.</param>
public sealed record SlotPlayer(int PlayerId, string Position, int? Inning, int OwnsFrom, int? SubstitutionId);

/// <summary>
/// Lists, adds and removes the substitutes of a team's saved lineup and the position changes of its players, and
/// works out who held a slot in each inning.
/// At bats stay keyed by slot and inning (<see cref="BattingNote"/>); a cell belongs to whoever held the slot then.
/// Each add returns a validation message, or null on success; a refused add changes nothing.
/// </summary>
public class SubstitutionService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string TeamNotInGameMessage = "This team is not playing in this game.";
    public const string SlotMessage = "That batting slot has no player yet: save the lineup first.";
    public const string NotOnRosterMessage = "That player is not on this roster.";
    public const string AlreadyInGameMessage = "That player is already in this team's lineup or has left the game.";
    public const string KindMessage = "Pick how the substitute came in: PH, PR or defensive.";

    public const string PositionChangeMessage = "Pick a fielding position or DH: PH and PR are not positions to move to.";
    public const string NotInGameMessage = "That player is not in the game in that inning.";
    public const string SameInningMessage = "That player already changes position in that inning: remove that change first.";

    public static string InningMessage(int innings) => $"The inning must be from 1 to {innings}.";

    public static string BeforeEnteredMessage(int entered) =>
        $"That player came into the game in inning {entered}: pick inning {entered} or later.";

    /// <summary>The team's substitutions in this game, in the order they were added (empty with no saved lineup).</summary>
    public async Task<List<Substitution>> ListAsync(int gameId, int rosterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Substitutions.AsNoTracking()
            .Where(s => db.Lineups.Any(l => l.Id == s.LineupId && l.GameId == gameId && l.RosterId == rosterId))
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Puts a bench player into a filled batting slot from the given inning. Refused when the player is a starter,
    /// the DH lineup's starting pitcher, or already came in (so is in the game or has left it).
    /// </summary>
    public async Task<string?> AddAsync(int gameId, int rosterId, int battingOrder, int playerId, SubstitutionKind kind,
        int inning)
    {
        if (!Enum.IsDefined(kind))
        {
            return KindMessage;
        }
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null || (game.AwayRosterId != rosterId && game.HomeRosterId != rosterId))
        {
            return TeamNotInGameMessage;
        }
        if (inning < 1 || inning > game.Innings)
        {
            return InningMessage(game.Innings);
        }
        var lineup = await db.Lineups.AsNoTracking().Include(l => l.Slots)
            .FirstOrDefaultAsync(l => l.GameId == gameId && l.RosterId == rosterId);
        if (lineup is null || !lineup.Slots.Any(s => s.BattingOrder == battingOrder))
        {
            return SlotMessage;
        }
        if (!await db.Players.AnyAsync(p => p.Id == playerId && p.RosterId == rosterId))
        {
            return NotOnRosterMessage;
        }
        if (lineup.Slots.Any(s => s.PlayerId == playerId) || lineup.StartingPitcherId == playerId
            || await db.Substitutions.AnyAsync(s => s.LineupId == lineup.Id && s.PlayerId == playerId))
        {
            return AlreadyInGameMessage;
        }

        db.Substitutions.Add(new Substitution
        {
            LineupId = lineup.Id, BattingOrder = battingOrder, PlayerId = playerId, Kind = kind, Inning = inning,
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another add of the same player; the unique index caught it.
            return AlreadyInGameMessage;
        }
        return null;
    }

    /// <summary>
    /// Removes one substitution of the game, and the substitute's position changes with it, since he is on the bench
    /// again. A row already gone (or of another game) is left alone.
    /// </summary>
    public async Task RemoveAsync(int gameId, int substitutionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var sub = await db.Substitutions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == substitutionId && db.Lineups.Any(l => l.Id == s.LineupId && l.GameId == gameId));
        if (sub is null)
        {
            return;
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.PositionChanges.Where(c => c.LineupId == sub.LineupId && c.PlayerId == sub.PlayerId).ExecuteDeleteAsync();
        await db.Substitutions.Where(s => s.Id == substitutionId).ExecuteDeleteAsync();
        await tx.CommitAsync();
    }

    /// <summary>The team's position changes in this game, in the order they were added (empty with no saved lineup).</summary>
    public async Task<List<PositionChange>> ListPositionChangesAsync(int gameId, int rosterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.PositionChanges.AsNoTracking()
            .Where(c => db.Lineups.Any(l => l.Id == c.LineupId && l.GameId == gameId && l.RosterId == rosterId))
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Gives a player who is in the game in that inning (a starter, or a substitute from the inning he came in until
    /// someone takes his slot) a new fielding position from that inning. Refused for PH, PR, or one more change of
    /// the same player in the same inning.
    /// </summary>
    public async Task<string?> AddPositionChangeAsync(int gameId, int rosterId, int playerId, Position position, int inning)
    {
        if (!Enum.IsDefined(position) || position is Position.PinchHitter or Position.PinchRunner)
        {
            return PositionChangeMessage;
        }
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null || (game.AwayRosterId != rosterId && game.HomeRosterId != rosterId))
        {
            return TeamNotInGameMessage;
        }
        if (inning < 1 || inning > game.Innings)
        {
            return InningMessage(game.Innings);
        }
        var lineup = await db.Lineups.AsNoTracking().Include(l => l.Slots)
            .FirstOrDefaultAsync(l => l.GameId == gameId && l.RosterId == rosterId);
        if (lineup is null)
        {
            return NotInGameMessage;
        }
        var subs = await db.Substitutions.AsNoTracking().Where(s => s.LineupId == lineup.Id).ToListAsync();
        var slot = lineup.Slots.Select(starter => SlotPlayers(starter, subs))
            .FirstOrDefault(players => players.Any(p => p.PlayerId == playerId));
        if (slot is null)
        {
            return NotInGameMessage;
        }
        var at = slot.FindIndex(p => p.PlayerId == playerId);
        var entered = slot[at].Inning ?? 1;
        if (inning < entered)
        {
            return BeforeEnteredMessage(entered);
        }
        if (at + 1 < slot.Count && slot[at + 1].Inning <= inning)
        {
            return NotInGameMessage;
        }
        if (await db.PositionChanges.AnyAsync(c => c.LineupId == lineup.Id && c.PlayerId == playerId && c.Inning == inning))
        {
            return SameInningMessage;
        }

        db.PositionChanges.Add(new PositionChange { LineupId = lineup.Id, PlayerId = playerId, Position = position, Inning = inning });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another change of the same player in the same inning; the unique index caught it.
            return SameInningMessage;
        }
        return null;
    }

    /// <summary>Removes one position change of the game. A row already gone (or of another game) is left alone.</summary>
    public async Task RemovePositionChangeAsync(int gameId, int positionChangeId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.PositionChanges
            .Where(c => c.Id == positionChangeId && db.Lineups.Any(l => l.Id == c.LineupId && l.GameId == gameId))
            .ExecuteDeleteAsync();
    }

    /// <summary>The Pos column of one player: his first position, then each change of his in inning order, like "PH, CF".</summary>
    public static string PositionText(SlotPlayer player, IEnumerable<PositionChange> changes) =>
        string.Join(", ", changes.Where(c => c.PlayerId == player.PlayerId).OrderBy(c => c.Inning).ThenBy(c => c.Id)
            .Select(c => Positions.Abbreviation(c.Position)).Prepend(player.Position).Where(p => p != ""));

    /// <summary>
    /// The slot's players: its starter, then its substitutes in the order they took the slot (entry inning, then
    /// the order they were added). A PH or defensive sub owns at bats from his inning; a PR, or anyone coming in
    /// the same inning right after a PH or PR, from the next one, since the slot already batted that inning.
    /// </summary>
    public static List<SlotPlayer> SlotPlayers(LineupSlot starter, IEnumerable<Substitution> substitutions)
    {
        var starterPosition = starter.Position == 0 ? "" : Positions.Abbreviation(starter.Position);
        var players = new List<SlotPlayer> { new(starter.PlayerId, starterPosition, null, 1, null) };
        Substitution? previous = null;
        foreach (var s in substitutions.Where(s => s.BattingOrder == starter.BattingOrder)
                     .OrderBy(s => s.Inning).ThenBy(s => s.Id))
        {
            var afterPinchInSameInning = previous is { Kind: SubstitutionKind.PinchHitter or SubstitutionKind.PinchRunner }
                && previous.Inning == s.Inning;
            var ownsFrom = s.Kind == SubstitutionKind.PinchRunner || afterPinchInSameInning ? s.Inning + 1 : s.Inning;
            var position = s.Kind switch
            {
                SubstitutionKind.PinchHitter => Positions.Abbreviation(Position.PinchHitter),
                SubstitutionKind.PinchRunner => Positions.Abbreviation(Position.PinchRunner),
                // A defensive sub starts at the starter's position; a PositionChange moves him from there.
                _ => starterPosition,
            };
            players.Add(new SlotPlayer(s.PlayerId, position, s.Inning, Math.Max(ownsFrom, players[^1].OwnsFrom), s.Id));
            previous = s;
        }
        return players;
    }

    /// <summary>The player whose at bat the slot's cell in this inning is: the last to take the slot by then (else the starter).</summary>
    public static SlotPlayer OwnerAt(IReadOnlyList<SlotPlayer> players, int inning) =>
        players.Last(p => p.OwnsFrom <= inning || p.Inning is null);
}
