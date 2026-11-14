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
        Services.AddScoped<PlayerService>();
        Services.AddScoped<LineupService>();
        Services.AddScoped<LineScoreService>();
        Services.AddScoped<BattingGridService>();
        Services.AddScoped<PitchingService>();
        Services.AddScoped<SubstitutionService>();
    }

    [Fact]
    public async Task Shows_the_matchup_and_puts_the_date_in_the_line_score_corner()
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
        cut.WaitForAssertion(() => Assert.Equal("April 2, 2026", cut.Find("#line-score thead th").TextContent));
        Assert.Equal(7 + 2, cut.FindAll("#line-score thead th").Count);
        Assert.Empty(cut.FindAll("#game-date, #game-innings, #game-batters"));
    }

    [Fact]
    public async Task Puts_the_away_pitchers_right_after_the_away_batting_table_and_home_pitchers_after_home_batting()
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

        cut.WaitForAssertion(() => Assert.Equal(
            new[] { "away-batting-grid", "away-pitchers", "home-batting-grid", "home-pitchers" },
            cut.FindAll("section").Select(s => s.Id).Where(id => id is "away-batting-grid" or "away-pitchers" or "home-batting-grid" or "home-pitchers")));
    }

    [Fact]
    public void Missing_game_says_it_was_not_found()
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, int.MaxValue));

        cut.WaitForAssertion(() => Assert.Contains("Game not found.", cut.Markup));
    }
}
