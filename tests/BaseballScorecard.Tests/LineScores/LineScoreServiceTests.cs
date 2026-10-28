using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.LineScores;

// Integration tests against SCORECARD_TEST_DB; each test makes its own game and rosters.
[Collection(PostgresCollection.Name)]
public class LineScoreServiceTests(PostgresFixture fixture)
{
    private readonly LineScoreService _lineScores = new(new FixtureDbFactory(fixture));

    private async Task<Game> NewGameAsync(int innings = 9)
    {
        var f = new FixtureDbFactory(fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = innings,
        };
        Assert.Null(await new GameService(f).CreateAsync(game));
        return game;
    }

    private async Task<Dictionary<(int RosterId, int Inning), int>> SavedAsync(int gameId) =>
        (await _lineScores.ListAsync(gameId)).ToDictionary(r => (r.RosterId, r.Inning), r => r.Runs);

    [Fact]
    public async Task Runs_save_per_team_and_inning_and_reload()
    {
        var g = await NewGameAsync();

        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 1, "2"));
        Assert.Null(await _lineScores.SaveAsync(g.Id, g.HomeRosterId, 1, "0"));
        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 9, "11"));

        var saved = await SavedAsync(g.Id);
        Assert.Equal(3, saved.Count);
        Assert.Equal(2, saved[(g.AwayRosterId, 1)]);
        Assert.Equal(0, saved[(g.HomeRosterId, 1)]);
        Assert.Equal(11, saved[(g.AwayRosterId, 9)]);
    }

    [Fact]
    public async Task Saving_an_inning_again_replaces_its_runs()
    {
        var g = await NewGameAsync();
        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 3, "1"));

        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 3, " 4 "));

        Assert.Equal(4, Assert.Single(await SavedAsync(g.Id)).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Blank_clears_the_inning(string? blank)
    {
        var g = await NewGameAsync();
        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 2, "3"));

        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 2, blank));

        Assert.Empty(await SavedAsync(g.Id));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("+2")]
    [InlineData("99999999999")]
    public async Task Negative_or_non_number_is_rejected_and_keeps_the_saved_runs(string bad)
    {
        var g = await NewGameAsync();
        Assert.Null(await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 1, "2"));

        Assert.Equal(LineScoreService.RunsMessage, await _lineScores.SaveAsync(g.Id, g.AwayRosterId, 1, bad));

        Assert.Equal(2, Assert.Single(await SavedAsync(g.Id)).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Inning_outside_the_game_is_rejected(int inning)
    {
        var g = await NewGameAsync(innings: 7);

        Assert.Equal(LineScoreService.InningMessage, await _lineScores.SaveAsync(g.Id, g.AwayRosterId, inning, "1"));

        Assert.Empty(await SavedAsync(g.Id));
    }

    [Fact]
    public async Task Team_not_in_the_game_is_rejected()
    {
        var g = await NewGameAsync();
        var other = await NewGameAsync();

        Assert.Equal(LineScoreService.TeamNotInGameMessage,
            await _lineScores.SaveAsync(g.Id, other.AwayRosterId, 1, "1"));

        Assert.Empty(await SavedAsync(g.Id));
    }

    [Fact]
    public async Task Runs_of_another_game_are_not_listed()
    {
        var g = await NewGameAsync();
        var other = await NewGameAsync();
        Assert.Null(await _lineScores.SaveAsync(other.Id, other.AwayRosterId, 1, "5"));

        Assert.Empty(await _lineScores.ListAsync(g.Id));
    }
}
