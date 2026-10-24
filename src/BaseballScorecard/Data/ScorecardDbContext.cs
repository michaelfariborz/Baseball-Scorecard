using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Data;

/// <summary>
/// EF Core context for the scorecard's Postgres database.
/// </summary>
public class ScorecardDbContext(DbContextOptions<ScorecardDbContext> options) : DbContext(options)
{
    public DbSet<Roster> Rosters => Set<Roster>();

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Roster>()
            .HasIndex(r => new { r.TeamName, r.Year })
            .IsUnique();

        // Restrict: a game keeps its teams; deleting a roster with games fails instead of deleting them.
        modelBuilder.Entity<Game>()
            .HasOne(g => g.AwayRoster).WithMany().HasForeignKey(g => g.AwayRosterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Game>()
            .HasOne(g => g.HomeRoster).WithMany().HasForeignKey(g => g.HomeRosterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
