using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Data;

public static class ScorecardDbServiceCollectionExtensions
{
    public const string ConnectionStringName = "Scorecard";

    /// <summary>
    /// Registers <see cref="ScorecardDbContext"/> (and its factory, for Blazor components) on Npgsql,
    /// reading <c>ConnectionStrings:Scorecard</c> from user-secrets or the
    /// <c>ConnectionStrings__Scorecard</c> env var. Never from appsettings*.json.
    /// </summary>
    public static IServiceCollection AddScorecardDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<ScorecardDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} is not set. In development run " +
                    $"`dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"<connection string>\" " +
                    $"--project src/BaseballScorecard`; elsewhere set the env var ConnectionStrings__{ConnectionStringName}.");
            }
            options.UseNpgsql(connectionString);
        });
        return services;
    }
}
