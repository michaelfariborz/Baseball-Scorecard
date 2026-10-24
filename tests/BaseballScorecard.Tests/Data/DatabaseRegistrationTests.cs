using System.Reflection;
using BaseballScorecard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Data;

public class DatabaseRegistrationTests
{
    private static ServiceProvider BuildServices(string? connectionString)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Scorecard"] = connectionString,
            })
            .Build();

        return new ServiceCollection().AddScorecardDb(config).BuildServiceProvider();
    }

    [Fact]
    public void Context_uses_Npgsql_with_the_Scorecard_connection_string()
    {
        using var services = BuildServices("Host=example;Database=scorecard_from_config");

        using var db = services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>().CreateDbContext();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        Assert.Contains("Database=scorecard_from_config", db.Database.GetConnectionString());
    }

    [Fact]
    public void Context_is_also_resolvable_directly_from_a_scope()
    {
        using var services = BuildServices("Host=example;Database=x");
        using var scope = services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ScorecardDbContext>());
    }

    [Fact]
    public void Missing_connection_string_fails_with_a_message_naming_the_key()
    {
        using var services = BuildServices(null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>().CreateDbContext());

        Assert.Contains("ConnectionStrings:Scorecard", ex.Message);
    }

    [Fact]
    public void App_project_has_a_user_secrets_id()
    {
        var attr = typeof(ScorecardDbContext).Assembly.GetCustomAttribute<UserSecretsIdAttribute>();

        Assert.False(string.IsNullOrWhiteSpace(attr?.UserSecretsId));
    }
}
