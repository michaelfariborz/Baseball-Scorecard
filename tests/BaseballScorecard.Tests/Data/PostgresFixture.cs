using BaseballScorecard.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseballScorecard.Tests.Data;

/// <summary>
/// Shared real-Postgres fixture. Reads the test connection string from SCORECARD_TEST_DB, creates
/// its own database named off it (so overlapping test runs never share rows), applies all
/// migrations before any test in the collection runs, and drops that database when the run ends.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string EnvVar = "SCORECARD_TEST_DB";

    public DbContextOptions<ScorecardDbContext> Options { get; private set; } = null!;

    public ScorecardDbContext CreateContext() => new(Options);

    public static string ReadConnectionString(Func<string, string?> getEnv)
    {
        var value = getEnv(EnvVar);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{EnvVar} is not set. Point it at a throwaway local Postgres database, e.g. " +
                $"{EnvVar}=\"Host=localhost;Database=baseball_scorecard_test;Username=<you>\".");
        }
        return value;
    }

    /// <summary>The same connection with a database unique to this run, e.g. baseball_scorecard_test_1a2b3c4d5e6f.</summary>
    // ponytail: a run killed before DisposeAsync leaves its database behind; drop strays by hand.
    public static string RunConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Database = $"{builder.Database ?? "scorecard_test"}_{Guid.NewGuid().ToString("N")[..12]}";
        return builder.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        var connectionString = RunConnectionString(ReadConnectionString(Environment.GetEnvironmentVariable));
        Options = new DbContextOptionsBuilder<ScorecardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
