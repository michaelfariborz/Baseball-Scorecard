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

    public DbSet<BattingNote> BattingNotes => Set<BattingNote>();

    public DbSet<InningHitsLob> InningHitsLob => Set<InningHitsLob>();

    public DbSet<GamePitcher> GamePitchers => Set<GamePitcher>();

    public DbSet<Substitution> Substitutions => Set<Substitution>();

    public DbSet<PositionChange> PositionChanges => Set<PositionChange>();

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

        // A player comes into a lineup once; the row goes with its lineup, or with the player off the roster.
        modelBuilder.Entity<Substitution>()
            .HasOne<Lineup>().WithMany().HasForeignKey(s => s.LineupId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Substitution>()
            .HasOne<Player>().WithMany().HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Substitution>()
            .HasIndex(s => new { s.LineupId, s.PlayerId })
            .IsUnique();

        // One position change per player per inning; the row goes with its lineup, or with the player off the roster.
        modelBuilder.Entity<PositionChange>()
            .HasOne<Lineup>().WithMany().HasForeignKey(c => c.LineupId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PositionChange>()
            .HasOne<Player>().WithMany().HasForeignKey(c => c.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PositionChange>()
            .HasIndex(c => new { c.LineupId, c.PlayerId, c.Inning })
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

        // One note per batting slot per inning, and one HITS / LOB pair per team per inning; removed with the game.
        modelBuilder.Entity<BattingNote>()
            .HasOne<Game>().WithMany().HasForeignKey(n => n.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BattingNote>()
            .HasOne<Roster>().WithMany().HasForeignKey(n => n.RosterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BattingNote>()
            .HasIndex(n => new { n.GameId, n.RosterId, n.BattingOrder, n.Inning })
            .IsUnique();
        modelBuilder.Entity<InningHitsLob>()
            .HasOne<Game>().WithMany().HasForeignKey(t => t.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InningHitsLob>()
            .HasOne<Roster>().WithMany().HasForeignKey(t => t.RosterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InningHitsLob>()
            .HasIndex(t => new { t.GameId, t.RosterId, t.Inning })
            .IsUnique();

        // A player pitches at most once per game; the row goes with its game, or with the player off the roster.
        modelBuilder.Entity<GamePitcher>()
            .HasOne<Game>().WithMany().HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GamePitcher>()
            .HasOne<Roster>().WithMany().HasForeignKey(p => p.RosterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GamePitcher>()
            .HasOne<Player>().WithMany().HasForeignKey(p => p.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GamePitcher>()
            .HasIndex(p => new { p.GameId, p.PlayerId })
            .IsUnique();
    }
}
