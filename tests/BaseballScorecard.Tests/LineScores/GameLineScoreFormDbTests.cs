using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.LineScores;

// The line score section of the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class GameLineScoreFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly LineScoreService _lineScores;

    public GameLineScoreFormDbTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
        Services.AddScoped<PlayerService>();
        Services.AddScoped<LineupService>();
        Services.AddScoped<LineScoreService>();
        _lineScores = new LineScoreService(new FixtureDbFactory(fixture));
    }

    private async Task<Game> NewGameAsync(int innings = 9)
    {
        var f = new FixtureDbFactory(_fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = innings,
        };
        Assert.Null(await new GameService(f).CreateAsync(game));
        game.AwayRoster = away;
        game.HomeRoster = home;
        return game;
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        cut.WaitForElement("#home-final");
        return cut;
    }

    /// <summary>Types into one cell, then waits until that save shows in the team's Final.</summary>
    private static void Type(IRenderedComponent<GamePage> cut, string side, int inning, string text,
        string? awayFinal = null, string? homeFinal = null)
    {
        cut.Find($"#{side}-inning-{inning}-runs").Change(text);
        cut.WaitForAssertion(() =>
        {
            if (awayFinal is not null) Assert.Equal(awayFinal, cut.Find("#away-final").TextContent.Trim());
            if (homeFinal is not null) Assert.Equal(homeFinal, cut.Find("#home-final").TextContent.Trim());
        });
    }

    private static string Cell(IRenderedComponent<GamePage> cut, string side, int inning) =>
        cut.Find($"#{side}-inning-{inning}-runs").GetAttribute("value") ?? "";

    [Fact]
    public async Task Line_score_has_innings_then_final_and_away_row_first()
    {
        var g = await NewGameAsync();

        var cut = RenderGame(g.Id);

        Assert.Equal(["", "1", "2", "3", "4", "5", "6", "7", "8", "9", "Final"],
            cut.FindAll("#line-score thead th").Select(th => th.TextContent.Trim()));
        var rows = cut.FindAll("#line-score tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Equal(g.AwayRoster!.TeamName, rows[0].QuerySelector("th")!.TextContent.Trim());
        Assert.Equal(g.HomeRoster!.TeamName, rows[1].QuerySelector("th")!.TextContent.Trim());
        Assert.NotNull(rows[0].QuerySelector("#away-inning-9-runs"));
        Assert.NotNull(rows[1].QuerySelector("#home-inning-9-runs"));
    }

    [Fact]
    public async Task Seven_inning_game_shows_exactly_seven_inning_columns()
    {
        var g = await NewGameAsync(innings: 7);

        var cut = RenderGame(g.Id);

        Assert.Equal(["", "1", "2", "3", "4", "5", "6", "7", "Final"],
            cut.FindAll("#line-score thead th").Select(th => th.TextContent.Trim()));
        Assert.Equal(7, cut.FindAll("#line-score tbody tr:first-child input").Count);
        Assert.Empty(cut.FindAll("#away-inning-8-runs"));
    }

    [Fact]
    public async Task Typed_runs_save_and_are_still_there_after_a_reload()
    {
        var g = await NewGameAsync();
        var cut = RenderGame(g.Id);

        Type(cut, "away", 1, "2", awayFinal: "2");
        Type(cut, "home", 3, "0", homeFinal: "0");

        Assert.Equal(2, (await _lineScores.ListAsync(g.Id)).Count);
        var reloaded = RenderGame(g.Id);
        Assert.Equal("2", Cell(reloaded, "away", 1));
        Assert.Equal("0", Cell(reloaded, "home", 3));
        Assert.Equal("", Cell(reloaded, "away", 2));
        Assert.Empty(reloaded.FindAll("#line-score-error"));
    }

    [Fact]
    public async Task Final_is_the_sum_and_blank_innings_stay_blank()
    {
        var g = await NewGameAsync();
        var cut = RenderGame(g.Id);
        Assert.Equal("", cut.Find("#away-final").TextContent.Trim());

        Type(cut, "away", 1, "2", awayFinal: "2");
        Type(cut, "away", 4, "3", awayFinal: "5");
        Type(cut, "home", 2, "1", homeFinal: "1");
        Assert.Equal("", Cell(cut, "away", 2));

        Type(cut, "away", 4, "", awayFinal: "2");

        var reloaded = RenderGame(g.Id);
        Assert.Equal("2", reloaded.Find("#away-final").TextContent.Trim());
        Assert.Equal("1", reloaded.Find("#home-final").TextContent.Trim());
        Assert.Equal("", Cell(reloaded, "away", 4));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("two")]
    public async Task Negative_or_non_number_is_not_saved_and_shows_the_message(string bad)
    {
        var g = await NewGameAsync();
        var cut = RenderGame(g.Id);
        Type(cut, "away", 1, "3", awayFinal: "3");

        cut.Find("#away-inning-1-runs").Change(bad);

        cut.WaitForAssertion(() =>
            Assert.Equal("Runs must be a whole number of 0 or more.", cut.Find("#line-score-error").TextContent.Trim()));
        Assert.Equal("3", cut.Find("#away-final").TextContent.Trim());
        Assert.Equal(bad, Cell(cut, "away", 1));
        Assert.Equal(3, Assert.Single(await _lineScores.ListAsync(g.Id)).Runs);

        Type(cut, "away", 1, "4", awayFinal: "4");
        Assert.Empty(cut.FindAll("#line-score-error"));
    }
}
