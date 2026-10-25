using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Lineups;

// The lineup section of the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class GameLineupFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly LineupService _lineups;

    private static readonly string[] NoDh =
        ["CenterField", "Shortstop", "LeftField", "FirstBase", "RightField", "ThirdBase", "Catcher", "SecondBase", "Pitcher"];

    private static readonly string[] WithDh =
        ["CenterField", "Shortstop", "LeftField", "FirstBase", "DesignatedHitter", "ThirdBase", "Catcher", "SecondBase", "RightField"];

    public GameLineupFormDbTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
        Services.AddScoped<PlayerService>();
        Services.AddScoped<LineupService>();
        _lineups = new LineupService(new FixtureDbFactory(fixture));
    }

    private sealed record Setup(int GameId, int AwayId, List<Player> Away, List<Player> Home);

    /// <summary>A game; the away roster has 11 players, the home roster 9.</summary>
    private async Task<Setup> NewGameAsync()
    {
        var f = new FixtureDbFactory(_fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id };
        Assert.Null(await new GameService(f).CreateAsync(game));

        async Task<List<Player>> AddAsync(int rosterId, string last, int count)
        {
            var list = new List<Player>();
            for (var i = 1; i <= count; i++)
            {
                var p = new Player { RosterId = rosterId, UniformNumber = $"{i}", FirstName = "First", LastName = $"{last}{i}" };
                Assert.Null(await new PlayerService(f).SaveAsync(p));
                list.Add(p);
            }
            return list;
        }

        return new Setup(game.Id, away.Id, await AddAsync(away.Id, "Cub", 11), await AddAsync(home.Id, "Sox", 9));
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        cut.WaitForElement("#away-slot-9-position");
        cut.WaitForElement("#home-slot-9-position");
        return cut;
    }

    private static void Fill(IRenderedComponent<GamePage> cut, List<Player> players, string[] positions)
    {
        for (var i = 0; i < positions.Length; i++)
        {
            cut.Find($"#away-slot-{i + 1}-player").Change($"{players[i].Id}");
            cut.Find($"#away-slot-{i + 1}-position").Change(positions[i]);
        }
    }

    private static List<string> OptionTexts(IRenderedComponent<GamePage> cut, string selector) =>
        cut.FindAll($"{selector} option").Select(o => o.TextContent).Where(t => t != "").ToList();

    [Fact]
    public async Task Each_team_has_nine_slots_picking_its_own_players_and_numbered_positions()
    {
        var s = await NewGameAsync();

        var cut = RenderGame(s.GameId);

        foreach (var side in new[] { "away", "home" })
        {
            Assert.Equal(9, cut.FindAll($"#lineup-{side} tbody tr").Count);
            Assert.Equal(
                ["1 · Pitcher", "2 · Catcher", "3 · First Base", "4 · Second Base", "5 · Third Base", "6 · Shortstop",
                 "7 · Left Field", "8 · Center Field", "9 · Right Field", "10 · Designated Hitter"],
                OptionTexts(cut, $"#{side}-slot-5-position"));
        }
        Assert.Equal(s.Away.Select(p => $"#{p.UniformNumber} {p.FirstName} {p.LastName}"), OptionTexts(cut, "#away-slot-1-player"));
        Assert.Equal(s.Home.Select(p => $"#{p.UniformNumber} {p.FirstName} {p.LastName}"), OptionTexts(cut, "#home-slot-1-player"));
        Assert.Empty(cut.FindAll("#away-starting-pitcher"));
    }

    [Fact]
    public async Task Saved_lineup_reloads_in_batting_order()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.GameId);
        var order = new[] { 4, 0, 8, 1, 2, 3, 5, 6, 7 }.Select(i => s.Away[i]).ToList();

        Fill(cut, order, NoDh);
        cut.Find("#away-save-lineup").Click();

        cut.WaitForAssertion(() => Assert.Equal("Lineup saved.", cut.Find("#away-lineup-saved").TextContent));
        Assert.Equal(order.Select(p => p.Id), (await _lineups.FindAsync(s.GameId, s.AwayId))!.Slots.Select(x => x.PlayerId));

        var reloaded = RenderGame(s.GameId);
        reloaded.WaitForAssertion(() =>
            Assert.Equal($"{order[0].Id}", reloaded.Find("#away-slot-1-player").GetAttribute("value")));
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal($"{order[i].Id}", reloaded.Find($"#away-slot-{i + 1}-player").GetAttribute("value"));
            Assert.Equal(NoDh[i], reloaded.Find($"#away-slot-{i + 1}-position").GetAttribute("value"));
        }
    }

    [Fact]
    public async Task Empty_slot_shows_a_message_naming_it_and_saves_nothing()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.GameId);

        Fill(cut, s.Away, NoDh[..5]);
        cut.Find("#away-save-lineup").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(LineupService.EmptySlotMessage(6), cut.Find("#away-lineup-error").TextContent));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.AwayId));
    }

    [Fact]
    public async Task Same_player_or_position_twice_shows_a_message_naming_the_slot_and_saves_nothing()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.GameId);
        Fill(cut, s.Away, NoDh);

        cut.Find("#away-slot-3-player").Change($"{s.Away[1].Id}");
        cut.Find("#away-save-lineup").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal(LineupService.SamePlayerMessage(3, 2), cut.Find("#away-lineup-error").TextContent));

        cut.Find("#away-slot-3-player").Change($"{s.Away[2].Id}");
        cut.Find("#away-slot-4-position").Change(NoDh[0]);
        cut.Find("#away-save-lineup").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal(LineupService.SamePositionMessage(4, 1), cut.Find("#away-lineup-error").TextContent));

        Assert.Null(await _lineups.FindAsync(s.GameId, s.AwayId));
    }

    [Fact]
    public async Task Dh_disables_pitcher_and_requires_a_non_batting_starting_pitcher()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.GameId);

        Fill(cut, s.Away, WithDh);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#away-starting-pitcher")));
        for (var i = 1; i <= 9; i++)
        {
            Assert.True(cut.Find($"#away-slot-{i}-position option[value=Pitcher]").HasAttribute("disabled"));
        }
        Assert.False(cut.Find("#home-slot-1-position option[value=Pitcher]").HasAttribute("disabled"));
        Assert.Equal(
            s.Away.Skip(9).Select(p => $"#{p.UniformNumber} {p.FirstName} {p.LastName}"),
            OptionTexts(cut, "#away-starting-pitcher"));

        cut.Find("#away-save-lineup").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal(LineupService.StartingPitcherRequiredMessage, cut.Find("#away-lineup-error").TextContent));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.AwayId));

        cut.Find("#away-starting-pitcher").Change($"{s.Away[10].Id}");
        cut.Find("#away-save-lineup").Click();
        cut.WaitForAssertion(() => Assert.Equal("Lineup saved.", cut.Find("#away-lineup-saved").TextContent));
        Assert.Equal(s.Away[10].Id, (await _lineups.FindAsync(s.GameId, s.AwayId))!.StartingPitcherId);

        var reloaded = RenderGame(s.GameId);
        reloaded.WaitForAssertion(() =>
            Assert.Equal($"{s.Away[10].Id}", reloaded.Find("#away-starting-pitcher").GetAttribute("value")));
    }
}
