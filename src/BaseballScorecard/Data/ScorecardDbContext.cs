using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Data;

/// <summary>
/// EF Core context for the scorecard's Postgres database.
/// </summary>
public class ScorecardDbContext(DbContextOptions<ScorecardDbContext> options) : DbContext(options)
{
    public DbSet<Roster> Rosters => Set<Roster>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Roster>()
            .HasIndex(r => new { r.TeamName, r.Year })
            .IsUnique();

        // A uniform number is unique within one roster; removing a roster removes its players.
        modelBuilder.Entity<Player>()
            .HasOne<Roster>().WithMany().HasForeignKey(p => p.RosterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Player>()
            .HasIndex(p => new { p.RosterId, p.UniformNumber })
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
