using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Games;

[Collection(PostgresCollection.Name)]
public class GamePageTests : BunitContext
{
    private readonly PostgresFixture _fixture;

    public GamePageTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
    }

    [Fact]
    public async Task Shows_the_date_away_at_home_innings_and_batters()
    {
        var rosters = new RosterService(new FixtureDbFactory(_fixture));
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await rosters.SaveAsync(away));
        Assert.Null(await rosters.SaveAsync(home));
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 7,
        };
        Assert.Null(await Services.GetRequiredService<GameService>().CreateAsync(game));

        var cut = Render<GamePage>(p => p.Add(x => x.Id, game.Id));

        cut.WaitForAssertion(() => Assert.Equal($"{away.TeamName} @ {home.TeamName}", cut.Find("h1").TextContent));
        Assert.Equal("April 2, 2026", cut.Find("#game-date").TextContent);
        Assert.Equal("7", cut.Find("#game-innings").TextContent);
        Assert.Equal("9", cut.Find("#game-batters").TextContent);
    }

    [Fact]
    public void Missing_game_says_it_was_not_found()
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, int.MaxValue));

        cut.WaitForAssertion(() => Assert.Contains("Game not found.", cut.Markup));
    }
}
