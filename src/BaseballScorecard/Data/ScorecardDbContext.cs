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

    public DbSet<Lineup> Lineups => Set<Lineup>();

    public DbSet<InningRuns> InningRuns => Set<InningRuns>();

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

        // One lineup per team per game, removed with its game.
        modelBuilder.Entity<Lineup>()
            .HasOne<Game>().WithMany().HasForeignKey(l => l.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Lineup>()
            .HasOne<Roster>().WithMany().HasForeignKey(l => l.RosterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Lineup>()
            .HasIndex(l => new { l.GameId, l.RosterId })
            .IsUnique();
        // Removing a player from the roster clears the pitcher pick and empties their slot, so the scorer re-picks.
        modelBuilder.Entity<Lineup>()
            .HasOne<Player>().WithMany().HasForeignKey(l => l.StartingPitcherId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LineupSlot>()
            .HasOne<Lineup>().WithMany(l => l.Slots).HasForeignKey(s => s.LineupId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LineupSlot>()
            .HasOne<Player>().WithMany().HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LineupSlot>()
            .HasIndex(s => new { s.LineupId, s.BattingOrder })
            .IsUnique();

        // One runs cell per team per inning, removed with its game.
        modelBuilder.Entity<InningRuns>()
            .HasOne<Game>().WithMany().HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InningRuns>()
            .HasOne<Roster>().WithMany().HasForeignKey(r => r.RosterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InningRuns>()
            .HasIndex(r => new { r.GameId, r.RosterId, r.Inning })
            .IsUnique();
    }
}
