using BaseballScorecard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Games;

// The page tests register GameService themselves; this pins that the real app host (Program.cs) does too.
public class GameServiceRegistrationTests
{
    [Fact]
    public void App_host_registers_GameService()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Scorecard"] = "Host=example;Database=unused" }));
        });
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GameService>());
    }
}
