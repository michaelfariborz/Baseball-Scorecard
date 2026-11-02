using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.BattingGrids;

// The batting grid section of the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class GameBattingGridFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly BattingGridService _grid;

    private static readonly Position[] Positions9 =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.RightField,
        Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.Pitcher,
    ];

    public GameBattingGridFormDbTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
        Services.AddScoped<PlayerService>();
        Services.AddScoped<LineupService>();
        Services.AddScoped<LineScoreService>();
        Services.AddScoped<BattingGridService>();
        _grid = new BattingGridService(new FixtureDbFactory(fixture));
    }

    private sealed record Setup(Game Game, List<Player> Away, List<Player> Home);

    /// <summary>A game with 9 players on each roster; only the away lineup is saved, in reverse roster order.</summary>
    private async Task<Setup> NewGameAsync(int innings = 9)
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

        async Task<List<Player>> AddAsync(int rosterId, string last)
        {
            var list = new List<Player>();
            for (var i = 1; i <= 9; i++)
            {
                var p = new Player { RosterId = rosterId, UniformNumber = $"{i}", FirstName = "First", LastName = $"{last}{i}" };
                Assert.Null(await new PlayerService(f).SaveAsync(p));
                list.Add(p);
            }
            return list;
        }

        var awayPlayers = await AddAsync(away.Id, "Cub");
        var homePlayers = await AddAsync(home.Id, "Sox");
        var batting = Enumerable.Reverse(awayPlayers).ToList();
        Assert.Null(await new LineupService(f).SaveAsync(new Lineup
        {
            GameId = game.Id,
            RosterId = away.Id,
            Slots = batting.Select((p, i) => new LineupSlot { PlayerId = p.Id, Position = Positions9[i] }).ToList(),
        }));
        return new Setup(game, batting, homePlayers);
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        // Loaded: each lineup picker shows slot 9, and each grid a table or the no-lineup message.
        cut.WaitForAssertion(() =>
        {
            foreach (var side in new[] { "away", "home" })
            {
                Assert.NotEmpty(cut.FindAll($"#{side}-slot-9-position"));
                Assert.NotEmpty(cut.FindAll($"#{side}-batting-grid table, #{side}-batting-grid-empty"));
            }
        });
        return cut;
    }

    private static string Value(IRenderedComponent<GamePage> cut, string id) =>
        cut.Find($"#{id}").GetAttribute("value") ?? "";

    [Fact]
    public async Task Saved_lineup_shows_nine_rows_in_batting_order_then_inning_columns()
    {
        var s = await NewGameAsync(innings: 7);

        var cut = RenderGame(s.Game.Id);

        Assert.Equal(["Number", "Name", "Position", "1", "2", "3", "4", "5", "6", "7"],
            cut.FindAll("#away-batting-grid thead th").Select(th => th.TextContent.Trim()));
        var rows = cut.FindAll("#away-batting-grid tbody tr.batter");
        Assert.Equal(9, rows.Count);
        for (var i = 0; i < 9; i++)
        {
            var cells = rows[i].QuerySelectorAll("td, th").Select(c => c.TextContent.Trim()).Take(3).ToList();
            Assert.Equal([s.Away[i].UniformNumber, $"First {s.Away[i].LastName}", Positions.Label(Positions9[i])], cells);
            Assert.Equal(7, rows[i].QuerySelectorAll("input").Length);
        }
        Assert.NotNull(cut.Find("#away-slot-9-inning-7-note"));
        Assert.Empty(cut.FindAll("#away-slot-1-inning-8-note"));
    }

    [Fact]
    public async Task Team_without_a_saved_lineup_shows_the_message_instead_of_a_grid()
    {
        var s = await NewGameAsync();

        var cut = RenderGame(s.Game.Id);

        Assert.Equal("Save a lineup to start scoring.", cut.Find("#home-batting-grid-empty").TextContent.Trim());
        Assert.Empty(cut.FindAll("#home-batting-grid table"));
        Assert.Empty(cut.FindAll("#away-batting-grid-empty"));
    }

    [Fact]
    public async Task Saving_a_lineup_on_the_page_shows_its_grid()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        for (var i = 0; i < 9; i++)
        {
            cut.Find($"#home-slot-{i + 1}-player").Change($"{s.Home[i].Id}");
            cut.Find($"#home-slot-{i + 1}-position").Change($"{Positions9[i]}");
        }
        cut.Find("#home-save-lineup").Click();

        cut.WaitForElement("#home-slot-1-inning-1-note");
        Assert.Empty(cut.FindAll("#home-batting-grid-empty"));
    }

    [Fact]
    public async Task Saving_one_lineup_keeps_the_other_teams_unsaved_picks()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        cut.Find("#home-slot-1-player").Change($"{s.Home[0].Id}");

        cut.Find("#away-save-lineup").Click();

        cut.WaitForElement("#away-lineup-saved");
        Assert.Equal($"{s.Home[0].Id}", cut.Find("#home-slot-1-player").GetAttribute("value"));
    }

    [Fact]
    public async Task Typed_note_saves_and_is_still_there_after_a_reload_and_clearing_removes_it()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        cut.Find("#away-slot-3-inning-2-note").Change("6-3");
        cut.WaitForAssertion(() => Assert.Equal("6-3", Value(cut, "away-slot-3-inning-2-note")));
        cut.Find("#away-slot-4-inning-2-note").Change("K");
        cut.WaitForAssertion(() => Assert.Equal("K", Value(cut, "away-slot-4-inning-2-note")));
        Assert.Equal(2, (await _grid.ListNotesAsync(s.Game.Id)).Count);

        var reloaded = RenderGame(s.Game.Id);
        Assert.Equal("6-3", Value(reloaded, "away-slot-3-inning-2-note"));
        Assert.Equal("K", Value(reloaded, "away-slot-4-inning-2-note"));
        Assert.Equal("", Value(reloaded, "away-slot-3-inning-3-note"));
        Assert.Equal("20", reloaded.Find("#away-slot-3-inning-2-note").GetAttribute("maxlength"));

        reloaded.Find("#away-slot-3-inning-2-note").Change("");
        reloaded.WaitForAssertion(() => Assert.Equal("", Value(reloaded, "away-slot-3-inning-2-note")));
        Assert.Single(await _grid.ListNotesAsync(s.Game.Id));
        Assert.Equal("", Value(RenderGame(s.Game.Id), "away-slot-3-inning-2-note"));
    }

    [Fact]
    public async Task Hits_and_lob_boxes_per_inning_save_and_reload()
    {
        var s = await NewGameAsync(innings: 7);
        var cut = RenderGame(s.Game.Id);

        var totals = cut.Find("#away-batting-grid tbody tr.totals");
        Assert.Equal(7, totals.QuerySelectorAll("input[id$='-hits']").Length);
        Assert.Equal(7, totals.QuerySelectorAll("input[id$='-lob']").Length);

        cut.Find("#away-inning-1-hits").Change("2");
        cut.WaitForAssertion(() => Assert.Equal("2", Value(cut, "away-inning-1-hits")));
        cut.Find("#away-inning-1-lob").Change("0");
        cut.WaitForAssertion(() => Assert.Equal("0", Value(cut, "away-inning-1-lob")));
        Assert.True(await _grid.ListHitsLobAsync(s.Game.Id) is [{ Hits: 2, Lob: 0 }]);

        var reloaded = RenderGame(s.Game.Id);
        Assert.Equal("2", Value(reloaded, "away-inning-1-hits"));
        Assert.Equal("0", Value(reloaded, "away-inning-1-lob"));
        Assert.Equal("", Value(reloaded, "away-inning-2-hits"));
    }

    [Theory]
    [InlineData("hits", "-1")]
    [InlineData("lob", "two")]
    public async Task Bad_hits_or_lob_is_not_saved_and_shows_the_message(string box, string bad)
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        cut.Find($"#away-inning-1-{box}").Change("3");
        cut.WaitForAssertion(() => Assert.Equal("3", Value(cut, $"away-inning-1-{box}")));

        cut.Find($"#away-inning-1-{box}").Change(bad);

        cut.WaitForAssertion(() => Assert.Equal("HITS and LOB must be whole numbers of 0 or more.",
            cut.Find("#away-batting-grid-error").TextContent.Trim()));
        Assert.Equal(bad, Value(cut, $"away-inning-1-{box}"));
        var row = Assert.Single(await _grid.ListHitsLobAsync(s.Game.Id));
        Assert.Equal(3, box == "hits" ? row.Hits : row.Lob);

        cut.Find($"#away-inning-1-{box}").Change("4");
        cut.WaitForAssertion(() => Assert.Equal("4", Value(cut, $"away-inning-1-{box}")));
        Assert.Empty(cut.FindAll("#away-batting-grid-error"));
    }

    [Fact]
    public async Task A_good_save_elsewhere_clears_the_message_and_shows_what_is_saved_in_the_refused_box()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        cut.Find("#away-inning-1-hits").Change("-1");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#away-batting-grid-error")));

        cut.Find("#away-inning-2-lob").Change("1");

        cut.WaitForAssertion(() => Assert.Equal("1", Value(cut, "away-inning-2-lob")));
        Assert.Empty(cut.FindAll("#away-batting-grid-error"));
        Assert.Equal("", Value(cut, "away-inning-1-hits"));
    }
}
