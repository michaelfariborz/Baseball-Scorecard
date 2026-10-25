using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Data;

public static class ScorecardDbMigrator
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>
    /// Applies pending EF migrations one at a time, logging each, unless
    /// <c>Database:MigrateOnStartup</c> is false. A failing migration throws an error naming it,
    /// so the app stops instead of running on a half-migrated schema.
    /// EF Core 9+ takes a database-wide lock while migrating, so concurrent starts wait rather than collide.
    /// </summary>
    public static async Task MigrateScorecardDbAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ScorecardDbMigrator).FullName!);

        if (!services.GetRequiredService<IConfiguration>().GetValue(MigrateOnStartupKey, true))
        {
            logger.LogInformation("{Key} is false; not applying migrations on startup.", MigrateOnStartupKey);
            return;
        }

        await using var db = await services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>()
            .CreateDbContextAsync(cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is current; nothing to apply.");
            return;
        }

        // One forward-only MigrateAsync: targeting each migration by name could roll back
        // newer migrations another instance applied in the meantime.
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Each migration commits in its own transaction, so the first one still pending is the one that failed.
            var failed = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).FirstOrDefault() ?? pending[0];
            LogApplied(logger, pending.TakeWhile(m => m != failed));
            throw new InvalidOperationException($"Migration {failed} failed; the app will not start on a partly migrated schema.", ex);
        }
        LogApplied(logger, pending);
    }

    private static void LogApplied(ILogger logger, IEnumerable<string> migrations)
    {
        foreach (var migration in migrations)
        {
            logger.LogInformation("Applied migration {Migration}.", migration);
        }
    }
}
