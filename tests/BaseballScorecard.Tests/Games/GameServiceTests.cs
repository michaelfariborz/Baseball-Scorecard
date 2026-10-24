using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.Games;

// Integration tests against SCORECARD_TEST_DB; unique team names keep runs from colliding.
[Collection(PostgresCollection.Name)]
public class GameServiceTests(PostgresFixture fixture)
{
    private readonly GameService _games = new(new FixtureDbFactory(fixture));
    private readonly RosterService _rosters = new(new FixtureDbFactory(fixture));

    private async Task<Roster> NewRosterAsync(string prefix, int year)
    {
        var roster = new Roster { TeamName = $"{prefix} {Guid.NewGuid():N}", Year = year };
        Assert.Null(await _rosters.SaveAsync(roster));
        return roster;
    }

    private async Task<int> CountGamesAsync()
    {
        await using var db = fixture.CreateContext();
        return db.Games.Count();
    }

    [Fact]
    public async Task Create_saves_the_game_with_both_teams()
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 7,
        };

        var error = await _games.CreateAsync(game);

        Assert.Null(error);
        Assert.NotEqual(0, game.Id);
        var saved = await _games.FindAsync(game.Id);
        Assert.Equal(new DateOnly(2026, 4, 2), saved!.Date);
        Assert.Equal(2026, saved.Year);
        Assert.Equal(away.TeamName, saved.AwayRoster!.TeamName);
        Assert.Equal(home.TeamName, saved.HomeRoster!.TeamName);
        Assert.Equal(7, saved.Innings);
        Assert.Equal(9, saved.Batters);
    }

    [Fact]
    public async Task Batters_is_always_saved_as_nine()
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9, Batters = 11,
        };

        Assert.Null(await _games.CreateAsync(game));

        Assert.Equal(9, (await _games.FindAsync(game.Id))!.Batters);
    }

    [Fact]
    public async Task Same_away_and_home_team_is_rejected_and_nothing_is_saved()
    {
        var team = await NewRosterAsync("Cubs", 2026);
        var before = await CountGamesAsync();

        var error = await _games.CreateAsync(new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = team.Id, HomeRosterId = team.Id, Innings = 9,
        });

        Assert.Equal(GameService.SameTeamMessage, error);
        Assert.Equal(before, await CountGamesAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Missing_team_is_rejected_and_nothing_is_saved(bool pickAway, bool pickHome)
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var before = await CountGamesAsync();

        var error = await _games.CreateAsync(new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = pickAway ? away.Id : 0, HomeRosterId = pickHome ? home.Id : 0, Innings = 9,
        });

        Assert.Equal(GameService.NoTeamMessage, error);
        Assert.Equal(before, await CountGamesAsync());
    }

    [Fact]
    public async Task Team_from_another_year_is_rejected_and_nothing_is_saved()
    {
        var away = await NewRosterAsync("Cubs", 2025);
        var home = await NewRosterAsync("Sox", 2026);
        var before = await CountGamesAsync();

        var error = await _games.CreateAsync(new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9,
        });

        Assert.Equal(GameService.NoTeamMessage, error);
        Assert.Equal(before, await CountGamesAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Innings_below_one_is_rejected_and_nothing_is_saved(int innings)
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var before = await CountGamesAsync();

        var error = await _games.CreateAsync(new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = innings,
        });

        Assert.Equal(GameService.InningsMessage, error);
        Assert.Equal(before, await CountGamesAsync());
    }

    [Fact]
    public async Task One_inning_is_the_smallest_game_that_saves()
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2),
            AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 1,
        };

        Assert.Null(await _games.CreateAsync(game));

        Assert.Equal(1, (await _games.FindAsync(game.Id))!.Innings);
    }

    [Fact]
    public async Task Rosters_for_year_lists_only_that_year_ordered_by_name()
    {
        var tag = Guid.NewGuid().ToString("N");
        foreach (var (name, year) in new[] { ("B", 2026), ("A", 2026), ("C", 2025) })
        {
            Assert.Null(await _rosters.SaveAsync(new Roster { TeamName = $"{tag} {name}", Year = year }));
        }

        var mine = (await _games.RostersForYearAsync(2026))
            .Where(r => r.TeamName.StartsWith(tag))
            .Select(r => r.TeamName[(tag.Length + 1)..])
            .ToList();

        Assert.Equal(["A", "B"], mine);
    }
}
