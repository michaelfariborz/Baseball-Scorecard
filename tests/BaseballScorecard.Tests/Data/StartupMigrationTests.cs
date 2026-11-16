using BaseballScorecard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BaseballScorecard.Tests.Data;

/// <summary>
/// Startup applies pending EF migrations. Each test that needs an empty database gets its own,
/// named off SCORECARD_TEST_DB and dropped afterwards.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StartupMigrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly string[] AllMigrations = ["20261024210657_AddRosters", "20261024213132_AddGames", "20261024223539_AddPlayers", "20261025014635_AddLineups", "20261028222906_AddInningRuns", "20261102011800_AddBattingGrid", "20261104032438_AddGamePitchers", "20261104144257_AddPitchingLines", "20261111020427_AddBasesReached", "20261111030303_AddFielders", "20261114024603_AddCaughtStealing", "20261114032255_AddSubstitutions", "20261114155814_AddPositionChanges", "20261116002642_AddStolenBases", "20261116005054_AddInningColumns", "20261116012716_AddWildPitchesPassedBalls"];

    private readonly string _emptyDb = new NpgsqlConnectionStringBuilder(
        PostgresFixture.ReadConnectionString(Environment.GetEnvironmentVariable))
    {
        Database = $"scorecard_migrate_{Guid.NewGuid():N}",
    }.ConnectionString;

    private readonly CapturingLoggerProvider _logs = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var db = ContextOn(_emptyDb);
        await db.Database.EnsureDeletedAsync();
    }

    private static ScorecardDbContext ContextOn(string connectionString) =>
        new(new DbContextOptionsBuilder<ScorecardDbContext>().UseNpgsql(connectionString).Options);

    private ServiceProvider Services(string connectionString, bool? migrateOnStartup = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scorecard"] = connectionString,
                ["Database:MigrateOnStartup"] = migrateOnStartup?.ToString(),
            })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddLogging(b => b.AddProvider(_logs))
            .AddScorecardDb(config)
            .BuildServiceProvider();
    }

    [Fact]
    public async Task Empty_database_gets_every_migration_and_each_is_logged()
    {
        await using var services = Services(_emptyDb);

        await services.MigrateScorecardDbAsync();

        await using var db = ContextOn(_emptyDb);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(AllMigrations, await db.Database.GetAppliedMigrationsAsync());
        foreach (var migration in AllMigrations)
        {
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains(migration));
        }
    }

    [Fact]
    public async Task Current_database_logs_that_there_is_nothing_to_apply()
    {
        await using var services = Services(fixture.CreateContext().Database.GetConnectionString()!);

        await services.MigrateScorecardDbAsync();

        Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("nothing to apply"));
    }

    [Fact]
    public async Task MigrateOnStartup_false_applies_nothing()
    {
        await using var services = Services(_emptyDb, migrateOnStartup: false);

        await services.MigrateScorecardDbAsync();

        await using var db = ContextOn(_emptyDb);
        Assert.False(await db.GetService<IRelationalDatabaseCreator>().ExistsAsync());
    }

    [Fact]
    public async Task A_failing_migration_stops_with_an_error_naming_it()
    {
        await using (var db = ContextOn(_emptyDb))
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            // A table the AddRosters migration also creates, so that migration fails.
            await db.Database.ExecuteSqlRawAsync("""CREATE TABLE "Rosters" ("Id" integer)""");
        }
        await using var services = Services(_emptyDb);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => services.MigrateScorecardDbAsync());

        Assert.Contains("20261024210657_AddRosters", ex.Message);
        await using var check = ContextOn(_emptyDb);
        Assert.Empty(await check.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task A_failing_later_migration_is_named_and_earlier_ones_stay_applied_and_logged()
    {
        await using (var db = ContextOn(_emptyDb))
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            // A table the AddGames migration also creates: AddRosters applies, AddGames fails.
            await db.Database.ExecuteSqlRawAsync("""CREATE TABLE "Games" ("Id" integer)""");
        }
        await using var services = Services(_emptyDb);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => services.MigrateScorecardDbAsync());

        Assert.Contains("20261024213132_AddGames", ex.Message);
        Assert.DoesNotContain("20261024210657_AddRosters", ex.Message);
        await using var check = ContextOn(_emptyDb);
        Assert.Equal(["20261024210657_AddRosters"], await check.Database.GetAppliedMigrationsAsync());
        Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("20261024210657_AddRosters"));
        Assert.DoesNotContain(_logs.Entries, e => e.Message.Contains("Applied migration 20261024213132_AddGames"));
    }

    [Fact]
    public async Task At_bats_saved_before_AddStolenBases_load_unchanged_with_no_steals_after_it_applies()
    {
        await using var services = Services(_emptyDb);
        await services.MigrateScorecardDbAsync();
        var factory = services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>();
        var away = new Roster { TeamName = "Cubs", Year = 2026 };
        var home = new Roster { TeamName = "Sox", Year = 2026 };
        Assert.Null(await new RosterService(factory).SaveAsync(away));
        Assert.Null(await new RosterService(factory).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9 };
        Assert.Null(await new GameService(factory).CreateAsync(game));
        var grid = new BattingGridService(factory);
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 1, 1, "1B", 1, caughtStealingBase: 2, caughtStealingFieldersText: "26"));
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 2, 1, "2B", 2));
        // Back to the schema before this change: the rows now have no StolenBases column at all.
        await using (var db = ContextOn(_emptyDb))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261114155814_AddPositionChanges");
            Assert.Equal(["20261116002642_AddStolenBases", "20261116005054_AddInningColumns", "20261116012716_AddWildPitchesPassedBalls"], await db.Database.GetPendingMigrationsAsync());
        }

        await services.MigrateScorecardDbAsync();

        var notes = (await grid.ListNotesAsync(game.Id)).OrderBy(n => n.BattingOrder).ToList();
        Assert.Equal([("1B", 1, 2, "2-6"), ("2B", 2, 0, "")],
            notes.Select(n => (n.Note, n.BasesReached, n.CaughtStealingBase, n.CaughtStealingFielders)));
        Assert.All(notes, n => Assert.Empty(n.StolenBases));
        Assert.All(notes, n => Assert.Equal("", AtBatResult.StolenBasesText(n)));
    }

    [Fact]
    public async Task At_bats_saved_before_AddInningColumns_land_in_the_first_column_after_it_applies()
    {
        await using var services = Services(_emptyDb);
        await services.MigrateScorecardDbAsync();
        var factory = services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>();
        var away = new Roster { TeamName = "Cubs", Year = 2026 };
        var home = new Roster { TeamName = "Sox", Year = 2026 };
        Assert.Null(await new RosterService(factory).SaveAsync(away));
        Assert.Null(await new RosterService(factory).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9 };
        Assert.Null(await new GameService(factory).CreateAsync(game));
        var grid = new BattingGridService(factory);
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 1, 3, "1B", 1));
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 2, 3, "K"));
        // Back to the schema before this change: the rows now have no Column column at all.
        await using (var db = ContextOn(_emptyDb))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261116002642_AddStolenBases");
            Assert.Equal(["20261116005054_AddInningColumns", "20261116012716_AddWildPitchesPassedBalls"], await db.Database.GetPendingMigrationsAsync());
        }

        await services.MigrateScorecardDbAsync();

        var notes = (await grid.ListNotesAsync(game.Id)).OrderBy(n => n.BattingOrder).ToList();
        Assert.Equal([("1B", 1, 1), ("K", 2, 1)], notes.Select(n => (n.Note, n.BattingOrder, n.Column)));
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 1, 3, "2B", 2));
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 1, 3, "GO", column: 2));
        Assert.Equal([(1, "2B"), (2, "GO")],
            (await grid.ListNotesAsync(game.Id)).Where(n => n.BattingOrder == 1).OrderBy(n => n.Column).Select(n => (n.Column, n.Note)));
    }

    [Fact]
    public async Task At_bats_saved_before_AddWildPitchesPassedBalls_load_unchanged_with_no_marks_after_it_applies()
    {
        await using var services = Services(_emptyDb);
        await services.MigrateScorecardDbAsync();
        var factory = services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>();
        var away = new Roster { TeamName = "Cubs", Year = 2026 };
        var home = new Roster { TeamName = "Sox", Year = 2026 };
        Assert.Null(await new RosterService(factory).SaveAsync(away));
        Assert.Null(await new RosterService(factory).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9 };
        Assert.Null(await new GameService(factory).CreateAsync(game));
        var grid = new BattingGridService(factory);
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 1, 1, "1B", 3, stolenBases: [2, 3]));
        Assert.Null(await grid.SaveNoteAsync(game.Id, away.Id, 2, 1, "K"));
        // Back to the schema before this change: the rows now have no WildPitchBases or PassedBallBases columns.
        await using (var db = ContextOn(_emptyDb))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261116005054_AddInningColumns");
            Assert.Equal(["20261116012716_AddWildPitchesPassedBalls"], await db.Database.GetPendingMigrationsAsync());
        }

        await services.MigrateScorecardDbAsync();

        var notes = (await grid.ListNotesAsync(game.Id)).OrderBy(n => n.BattingOrder).ToList();
        Assert.Equal([("1B", 3, "SB 2,3"), ("K", 0, "")],
            notes.Select(n => (AtBatResult.CellText(n), n.BasesReached, AtBatResult.StolenBasesText(n))));
        Assert.All(notes, n => Assert.Empty(n.WildPitchBases));
        Assert.All(notes, n => Assert.Empty(n.PassedBallBases));
        Assert.All(notes, n => Assert.Equal("", AtBatResult.WildPitchText(n) + AtBatResult.PassedBallText(n)));
    }

    [Fact]
    public async Task Starting_the_app_host_migrates_an_empty_database()
    {
        using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Scorecard"] = _emptyDb }));
        }))
        {
            _ = factory.Services;
        }

        await using var db = ContextOn(_emptyDb);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(AllMigrations, await db.Database.GetAppliedMigrationsAsync());
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose() { }

    private sealed class Logger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (owner.Entries)
            {
                owner.Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
