using BaseballScorecard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Data;

/// <summary>
/// The real app host (Program.cs) registers the scorecard database from ConnectionStrings:Scorecard.
/// </summary>
public class AppStartupDatabaseTests
{
    [Fact]
    public void App_host_registers_ScorecardDbContext_on_Npgsql_from_the_Scorecard_connection_string()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Scorecard"] = "Host=example;Database=scorecard_from_app_host",
                }));
        });

        using var db = factory.Services.GetRequiredService<IDbContextFactory<ScorecardDbContext>>().CreateDbContext();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        Assert.Contains("Database=scorecard_from_app_host", db.Database.GetConnectionString());
    }
}
