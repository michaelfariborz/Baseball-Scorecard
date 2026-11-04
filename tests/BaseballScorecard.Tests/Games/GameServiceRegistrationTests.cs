using BaseballScorecard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Games;

// The page tests register GameService, LineupService, LineScoreService, BattingGridService and PitchingService themselves; this pins that the real app host (Program.cs) does too.
public class GameServiceRegistrationTests
{
    [Fact]
    public void App_host_registers_the_game_page_services()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Scorecard"] = "Host=example;Database=unused",
                    ["Database:MigrateOnStartup"] = "false",
                }));
        });
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GameService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LineupService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LineScoreService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BattingGridService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PitchingService>());
    }
}
