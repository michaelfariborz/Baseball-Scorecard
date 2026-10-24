using BaseballScorecard.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Tests.Data;

/// <summary>
/// Shared real-Postgres fixture. Reads the test database connection string from
/// SCORECARD_TEST_DB and applies all migrations before any test in the collection runs.
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

    public async Task InitializeAsync()
    {
        var connectionString = ReadConnectionString(Environment.GetEnvironmentVariable);
        Options = new DbContextOptionsBuilder<ScorecardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
