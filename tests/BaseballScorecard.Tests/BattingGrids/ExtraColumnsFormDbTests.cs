using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.BattingGrids;

// Extra columns for an inning when a team bats around, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class ExtraColumnsFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly BattingGridService _grid;

    public ExtraColumnsFormDbTests(PostgresFixture fixture)
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
        _grid = new BattingGridService(new FixtureDbFactory(fixture));
    }

    /// <summary>A 5-inning game where both teams have a saved lineup.</summary>
    private async Task<Game> NewGameAsync()
    {
        var f = new FixtureDbFactory(_fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 5,
        };
        Assert.Null(await new GameService(f).CreateAsync(game));

        foreach (var (rosterId, last) in new[] { (away.Id, "Cub"), (home.Id, "Sox") })
        {
            var slots = new List<LineupSlot>();
            for (var i = 1; i <= 9; i++)
            {
                var p = new Player { RosterId = rosterId, UniformNumber = $"{i}", FirstName = "First", LastName = $"{last}{i}" };
                Assert.Null(await new PlayerService(f).SaveAsync(p));
                slots.Add(new LineupSlot { PlayerId = p.Id, Position = (Position)i });
            }
            Assert.Null(await new LineupService(f).SaveAsync(new Lineup { GameId = game.Id, RosterId = rosterId, Slots = slots }));
        }
        return game;
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("#away-slot-1-inning-1-note"));
            Assert.NotEmpty(cut.FindAll("#home-slot-1-inning-1-note"));
        });
        return cut;
    }

    private static List<string> Headers(IRenderedComponent<GamePage> cut, string side) =>
        [.. cut.FindAll($"#{side}-batting-grid thead th").Select(th => th.TextContent.Trim())];

    private static void AddColumn(IRenderedComponent<GamePage> cut, string side, int inning)
    {
        cut.Find($"#{side}-extra-inning").Change($"{inning}");
        cut.Find($"#{side}-extra-add").Click();
    }

    private static void RemoveColumn(IRenderedComponent<GamePage> cut, string side, int inning)
    {
        cut.Find($"#{side}-extra-inning").Change($"{inning}");
        cut.Find($"#{side}-extra-remove").Click();
    }

    [Fact]
    public async Task Adding_a_column_puts_a_second_header_for_the_inning_right_after_it_on_that_team_only()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        AddColumn(cut, "away", 3);

        cut.WaitForAssertion(() =>
            Assert.Equal(["#", "Name", "Pos", "Inn", "1", "2", "3", "3", "4", "5"], Headers(cut, "away")));
        Assert.All(cut.FindAll("#away-batting-grid thead th").Skip(4), th => Assert.Contains("inning-header", th.ClassList));
        Assert.Equal(6, cut.FindAll("#away-batting-grid tr.batter")[0].QuerySelectorAll("button.at-bat-cell").Length);
        Assert.NotNull(cut.Find("#away-slot-9-inning-3-col-2-note"));
        Assert.Equal(["#", "Name", "Pos", "Inn", "1", "2", "3", "4", "5"], Headers(cut, "home"));
        Assert.Empty(cut.FindAll("#home-slot-1-inning-3-col-2-note"));

        AddColumn(cut, "away", 3);
        cut.WaitForElement("#away-slot-1-inning-3-col-3-note");
        Assert.Equal(["#", "Name", "Pos", "Inn", "1", "2", "3", "3", "3", "4", "5"], Headers(cut, "away"));
    }

    [Fact]
    public async Task An_at_bat_in_an_extra_column_saves_on_its_own_and_both_show_after_a_reload()
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 1, 3, "1B", 1));
        var cut = RenderGame(game.Id);
        AddColumn(cut, "away", 3);

        cut.WaitForElement("#away-slot-1-inning-3-col-2-note").Click();
        cut.WaitForElement("#away-at-bat-dialog");
        Assert.Contains("inning 3", cut.Find("#away-at-bat-title").TextContent);
        Assert.Empty(cut.FindAll("#away-at-bat-dialog .at-bat-results input[checked]"));
        cut.FindAll("#away-at-bat-dialog .at-bat-results input[type=radio]").Single(r => r.GetAttribute("value") == "GO").Change("GO");
        cut.WaitForElement("#away-at-bat-fielders").Change("63");
        cut.Find("#away-at-bat-save").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#away-at-bat-dialog")));
        Assert.Equal("GO 6-3", cut.Find("#away-slot-1-inning-3-col-2-note .at-bat-note").TextContent.Trim());
        var notes = (await _grid.ListNotesAsync(game.Id)).OrderBy(n => n.Column).ToList();
        Assert.True(notes is [{ Column: 1, Note: "1B", BasesReached: 1 }, { Column: 2, Note: "GO", Fielders: "6-3", BasesReached: 0 }]);

        var reloaded = RenderGame(game.Id);
        Assert.Equal(["#", "Name", "Pos", "Inn", "1", "2", "3", "3", "4", "5"], Headers(reloaded, "away"));
        Assert.Equal("1B", reloaded.Find("#away-slot-1-inning-3-note .at-bat-note").TextContent.Trim());
        Assert.Equal("GO 6-3", reloaded.Find("#away-slot-1-inning-3-col-2-note .at-bat-note").TextContent.Trim());
        Assert.Equal(["#", "Name", "Pos", "Inn", "1", "2", "3", "4", "5"], Headers(reloaded, "home"));
    }

    [Fact]
    public async Task An_empty_extra_column_can_be_removed_and_is_not_kept_after_a_reload()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);
        AddColumn(cut, "away", 2);
        cut.WaitForElement("#away-slot-1-inning-2-col-2-note");

        RemoveColumn(cut, "away", 2);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#away-slot-1-inning-2-col-2-note")));
        Assert.Equal(["#", "Name", "Pos", "Inn", "1", "2", "3", "4", "5"], Headers(cut, "away"));

        AddColumn(cut, "away", 2);
        cut.WaitForElement("#away-slot-1-inning-2-col-2-note");
        var reloaded = RenderGame(game.Id);
        Assert.Empty(reloaded.FindAll("#away-slot-1-inning-2-col-2-note"));
    }

    [Fact]
    public async Task An_extra_column_holding_an_at_bat_cannot_be_removed_and_shows_after_a_reload()
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 4, 2, "K", column: 2));
        var cut = RenderGame(game.Id);
        Assert.Equal("K", cut.Find("#away-slot-4-inning-2-col-2-note .at-bat-note").TextContent.Trim());

        RemoveColumn(cut, "away", 2);

        cut.WaitForAssertion(() => Assert.Equal("That column holds at-bats. Clear them before removing it.",
            cut.Find("#away-batting-grid-error").TextContent.Trim()));
        Assert.NotNull(cut.Find("#away-slot-4-inning-2-col-2-note"));
        Assert.Single(await _grid.ListNotesAsync(game.Id));
        var reloaded = RenderGame(game.Id);
        Assert.NotNull(reloaded.Find("#away-slot-4-inning-2-col-2-note"));
    }

    [Fact]
    public async Task Removing_from_an_inning_with_no_extra_column_shows_a_message()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        RemoveColumn(cut, "away", 1);

        cut.WaitForAssertion(() => Assert.Equal("Inning 1 has no extra column to remove.",
            cut.Find("#away-batting-grid-error").TextContent.Trim()));
        Assert.NotNull(cut.Find("#away-slot-1-inning-1-note"));
    }

    [Fact]
    public async Task Hits_and_lob_stay_one_box_each_spanning_all_of_the_innings_columns()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);
        AddColumn(cut, "away", 3);
        AddColumn(cut, "away", 3);
        cut.WaitForElement("#away-slot-1-inning-3-col-3-note");

        var totals = cut.Find("#away-batting-grid tbody tr.totals");
        Assert.Equal(5, totals.QuerySelectorAll("input[id$='-hits']").Length);
        Assert.Equal("3", cut.Find("#away-inning-3-hits").ParentElement!.GetAttribute("colspan"));
        Assert.Equal("1", cut.Find("#away-inning-2-hits").ParentElement!.GetAttribute("colspan") ?? "1");

        cut.Find("#away-inning-3-hits").Change("4");
        cut.WaitForAssertion(() => Assert.Equal("4", cut.Find("#away-inning-3-hits").GetAttribute("value")));
        Assert.True(await _grid.ListHitsLobAsync(game.Id) is [{ Inning: 3, Hits: 4 }]);
    }
}
