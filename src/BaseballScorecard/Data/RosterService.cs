using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Data;

/// <summary>
/// Lists, loads and saves rosters. <see cref="SaveAsync"/> returns a validation message, or null on success.
/// </summary>
public class RosterService(IDbContextFactory<ScorecardDbContext> dbFactory)
{
    public const string BlankNameMessage = "Team Name is required.";
    public const string DuplicateMessage = "A roster with this Team Name and Year already exists.";

    public async Task<List<Roster>> ListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Rosters.AsNoTracking()
            .OrderByDescending(r => r.Year)
            .ThenBy(r => r.TeamName)
            .ToListAsync();
    }

    public async Task<Roster?> FindAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Rosters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>Creates the roster when <c>Id</c> is 0 (and sets its new Id), otherwise updates it.</summary>
    public async Task<string?> SaveAsync(Roster roster)
    {
        var name = roster.TeamName?.Trim() ?? "";
        if (name.Length == 0)
        {
            return BlankNameMessage;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Rosters.AnyAsync(r => r.Id != roster.Id && r.TeamName == name && r.Year == roster.Year))
        {
            return DuplicateMessage;
        }

        var entity = roster.Id == 0
            ? db.Rosters.Add(new Roster()).Entity
            : await db.Rosters.FindAsync(roster.Id)
              ?? throw new InvalidOperationException($"Roster {roster.Id} does not exist.");
        entity.TeamName = name;
        entity.Year = roster.Year;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with another save of the same pair; the unique index caught it.
            return DuplicateMessage;
        }

        roster.Id = entity.Id;
        roster.TeamName = name;
        return null;
    }
}
