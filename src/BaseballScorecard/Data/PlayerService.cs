using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Lists, saves and removes a roster's players. <see cref="SaveAsync"/> returns a validation message, or null on success.
/// </summary>
public class PlayerService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string BlankFieldMessage = "Uniform Number, First Name and Last Name are required.";
    public const string NumberFormatMessage = "Uniform Number must be 1 or 2 digits.";
    public const string DuplicateNumberMessage = "Another player on this roster already wears that Uniform Number.";

    /// <summary>The roster's players, sorted by uniform number value, then by its text ("0" before "00").</summary>
    public async Task<List<Player>> ListAsync(int rosterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var players = await db.Players.AsNoTracking().Where(p => p.RosterId == rosterId).ToListAsync();
        return players
            .OrderBy(p => int.Parse(p.UniformNumber))
            .ThenBy(p => p.UniformNumber, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Adds the player when <c>Id</c> is 0 (and sets its new Id), otherwise updates it. Fields are trimmed.</summary>
    public async Task<string?> SaveAsync(Player player)
    {
        var number = player.UniformNumber?.Trim() ?? "";
        var first = player.FirstName?.Trim() ?? "";
        var last = player.LastName?.Trim() ?? "";
        if (number.Length == 0 || first.Length == 0 || last.Length == 0)
        {
            return BlankFieldMessage;
        }
        if (number.Length > 2 || !number.All(char.IsAsciiDigit))
        {
            return NumberFormatMessage;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var entity = player.Id == 0
            ? db.Players.Add(new Player { RosterId = player.RosterId }).Entity
            : await db.Players.FindAsync(player.Id)
              ?? throw new InvalidOperationException($"Player {player.Id} does not exist.");

        if (await db.Players.AnyAsync(p => p.Id != player.Id && p.RosterId == entity.RosterId && p.UniformNumber == number))
        {
            return DuplicateNumberMessage;
        }

        entity.UniformNumber = number;
        entity.FirstName = first;
        entity.LastName = last;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another save of the same number; the unique index caught it.
            return DuplicateNumberMessage;
        }

        player.Id = entity.Id;
        player.RosterId = entity.RosterId;
        player.UniformNumber = number;
        player.FirstName = first;
        player.LastName = last;
        return null;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Players.Where(p => p.Id == id).ExecuteDeleteAsync();
    }
}
