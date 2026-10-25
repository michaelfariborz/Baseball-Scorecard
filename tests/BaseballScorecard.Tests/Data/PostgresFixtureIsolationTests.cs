using BaseballScorecard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BaseballScorecard.Tests.Data;

/// <summary>
/// Each test run gets its own database, named off SCORECARD_TEST_DB, so overlapping runs
/// (two worktrees, or a lane and a person) never read each other's rows.
/// </summary>
public class PostgresFixtureIsolationTests
{
    private static string BaseDatabase =>
        new NpgsqlConnectionStringBuilder(PostgresFixture.ReadConnectionString(Environment.GetEnvironmentVariable)).Database!;

    private static string DatabaseOf(PostgresFixture fixture)
    {
        using var db = fixture.CreateContext();
        return new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Database!;
    }

    private static async Task<bool> ExistsAsync(PostgresFixture fixture)
    {
        await using var db = fixture.CreateContext();
        return await db.GetService<IRelationalDatabaseCreator>().ExistsAsync();
    }

    [Fact]
    public void Run_database_is_named_off_the_variable_with_a_unique_suffix()
    {
        var first = new NpgsqlConnectionStringBuilder(
            PostgresFixture.RunConnectionString("Host=localhost;Database=scorecard_test;Username=me"));
        var second = new NpgsqlConnectionStringBuilder(
            PostgresFixture.RunConnectionString("Host=localhost;Database=scorecard_test;Username=me"));

        Assert.StartsWith("scorecard_test_", first.Database);
        Assert.NotEqual(first.Database, second.Database);
        Assert.Equal("localhost", first.Host);
        Assert.Equal("me", first.Username);
    }

    [Fact]
    public async Task Two_runs_at_the_same_time_each_get_their_own_migrated_database_and_drop_it()
    {
        var runA = new PostgresFixture();
        var runB = new PostgresFixture();

        await Task.WhenAll(runA.InitializeAsync(), runB.InitializeAsync());
        try
        {
            Assert.NotEqual(BaseDatabase, DatabaseOf(runA));
            Assert.NotEqual(DatabaseOf(runA), DatabaseOf(runB));

            await using (var a = runA.CreateContext())
            {
                a.Rosters.Add(new Roster { TeamName = "Only in run A", Year = 2026 });
                await a.SaveChangesAsync();
            }

            await using var b = runB.CreateContext();
            Assert.Empty(await b.Database.GetPendingMigrationsAsync());
            Assert.False(await b.Rosters.AnyAsync(r => r.TeamName == "Only in run A"));
        }
        finally
        {
            await Task.WhenAll(runA.DisposeAsync(), runB.DisposeAsync());
        }

        Assert.False(await ExistsAsync(runA));
        Assert.False(await ExistsAsync(runB));
    }
}
