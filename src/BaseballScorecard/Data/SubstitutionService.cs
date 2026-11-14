using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>One player who held a batting slot: the starter (Inning null) or a substitute.</summary>
/// <param name="Position">The Pos column: the starter's position, PH, PR, or for a defensive sub the starter's position.</param>
/// <param name="OwnsFrom">The first inning whose at bat is this player's.</param>
public sealed record SlotPlayer(int PlayerId, string Position, int? Inning, int OwnsFrom, int? SubstitutionId);

/// <summary>
/// Lists, adds and removes the substitutes of a team's saved lineup, and works out who held a slot in each inning.
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

    public static string InningMessage(int innings) => $"The inning must be from 1 to {innings}.";

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

    /// <summary>Removes one substitution of the game. A row already gone (or of another game) is left alone.</summary>
    public async Task RemoveAsync(int gameId, int substitutionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Substitutions
            .Where(s => s.Id == substitutionId && db.Lineups.Any(l => l.Id == s.LineupId && l.GameId == gameId))
            .ExecuteDeleteAsync();
    }

    /// <summary>The first inning whose at bat is the substitute's. A pinch runner replaces a runner who already batted that inning, so his start is the next one.</summary>
    public static int OwnsFrom(Substitution s) => s.Kind == SubstitutionKind.PinchRunner ? s.Inning + 1 : s.Inning;

    /// <summary>The slot's players: its starter, then its substitutes in the order they took the slot.</summary>
    public static List<SlotPlayer> SlotPlayers(LineupSlot starter, IEnumerable<Substitution> substitutions)
    {
        var starterPosition = starter.Position == 0 ? "" : Positions.Abbreviation(starter.Position);
        var players = new List<SlotPlayer> { new(starter.PlayerId, starterPosition, null, 1, null) };
        players.AddRange(substitutions
            .Where(s => s.BattingOrder == starter.BattingOrder)
            .OrderBy(OwnsFrom).ThenBy(s => s.Id)
            .Select(s => new SlotPlayer(s.PlayerId, s.Kind switch
            {
                SubstitutionKind.PinchHitter => Positions.Abbreviation(Position.PinchHitter),
                SubstitutionKind.PinchRunner => Positions.Abbreviation(Position.PinchRunner),
                // ponytail: position changes after entry are the follow-up card; until then a defensive sub plays the starter's position.
                _ => starterPosition,
            }, s.Inning, OwnsFrom(s), s.Id)));
        return players;
    }

    /// <summary>The player whose at bat the slot's cell in this inning is: the last to take the slot by then (else the starter).</summary>
    public static SlotPlayer OwnerAt(IReadOnlyList<SlotPlayer> players, int inning) =>
        players.Last(p => p.OwnsFrom <= inning || p.Inning is null);
}
