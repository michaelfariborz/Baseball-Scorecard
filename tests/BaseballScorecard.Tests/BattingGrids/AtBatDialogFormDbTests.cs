using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.BattingGrids;

// Recording an at bat from the pop-up dialog on the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class AtBatDialogFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly BattingGridService _grid;

    private static readonly string[] Codes = ["1B", "2B", "3B", "HR", "BB", "HBP", "K", "ꓘ", "GO", "FO", "SF", "FC", "E"];

    public AtBatDialogFormDbTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
        Services.AddScoped<PlayerService>();
        Services.AddScoped<LineupService>();
        Services.AddScoped<LineScoreService>();
        Services.AddScoped<BattingGridService>();
        Services.AddScoped<PitchingService>();
        _grid = new BattingGridService(new FixtureDbFactory(fixture));
    }

    /// <summary>A game where both teams have a saved lineup; slot n bats "First Cub{n}" / "First Sox{n}".</summary>
    private async Task<Game> NewGameAsync()
    {
        var f = new FixtureDbFactory(_fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9,
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

    private static string Cell(IRenderedComponent<GamePage> cut, string id) => cut.Find($"#{id}").TextContent.Trim();

    /// <summary>Clicks the cell of the side's slot and inning, then waits for that side's dialog to render.</summary>
    private static void Open(IRenderedComponent<GamePage> cut, string side, int slot, int inning)
    {
        cut.Find($"#{side}-slot-{slot}-inning-{inning}-note").Click();
        cut.WaitForElement($"#{side}-at-bat-dialog");
    }

    private static void WaitClosed(IRenderedComponent<GamePage> cut, string side) =>
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll($"#{side}-at-bat-dialog")));

    private static string? Checked(IRenderedComponent<GamePage> cut, string side) =>
        cut.FindAll($"#{side}-at-bat-dialog .at-bat-results input[type=radio]").SingleOrDefault(r => r.HasAttribute("checked"))?.GetAttribute("value");

    private static void Pick(IRenderedComponent<GamePage> cut, string side, string code)
    {
        cut.FindAll($"#{side}-at-bat-dialog .at-bat-results input[type=radio]").Single(r => r.GetAttribute("value") == code).Change(code);
        cut.WaitForAssertion(() => Assert.Equal(code, Checked(cut, side)));
    }

    private static string? ElementRef(IRenderedComponent<GamePage> cut, string selector) =>
        cut.Find(selector).GetAttribute("blazor:elementReference");

    [Theory]
    [InlineData("away", "Cub")]
    [InlineData("home", "Sox")]
    public async Task Clicking_a_cell_opens_a_dialog_naming_the_batter_and_inning(string side, string last)
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);
        Assert.Empty(cut.FindAll($"#{side}-at-bat-dialog"));
        Assert.Empty(cut.FindAll($"#{side}-batting-grid tr.batter input"));

        Open(cut, side, 3, 4);

        var dialog = cut.Find($"#{side}-at-bat-dialog");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        var title = cut.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent;
        Assert.Contains($"First {last}3", title);
        Assert.Contains("inning 4", title);
    }

    [Fact]
    public async Task Dialog_offers_exactly_the_thirteen_results_in_order()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        Open(cut, "away", 1, 1);

        Assert.Equal(Codes, cut.FindAll("#away-at-bat-dialog .at-bat-results input[type=radio]").Select(r => r.GetAttribute("value")));
        Assert.Contains("strikeout looking", cut.Find("#away-at-bat-dialog").TextContent);
        Assert.Contains("strikeout swinging", cut.Find("#away-at-bat-dialog").TextContent);
    }

    [Theory]
    [InlineData("2B")]
    [InlineData("ꓘ")]
    public async Task Picking_a_result_and_saving_shows_it_in_the_cell_and_after_a_reload(string code)
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        Open(cut, "away", 3, 2);
        Pick(cut, "away", code);
        cut.Find("#away-at-bat-save").Click();

        cut.WaitForAssertion(() => Assert.Equal(code, Cell(cut, "away-slot-3-inning-2-note")));
        WaitClosed(cut, "away");
        var note = Assert.Single(await _grid.ListNotesAsync(game.Id));
        Assert.Equal((game.AwayRosterId, 3, 2, code), (note.RosterId, note.BattingOrder, note.Inning, note.Note));

        var reloaded = RenderGame(game.Id);
        Assert.Equal(code, Cell(reloaded, "away-slot-3-inning-2-note"));
        Assert.Equal("", Cell(reloaded, "away-slot-3-inning-3-note"));
    }

    [Fact]
    public async Task Filled_cell_opens_with_its_result_selected_and_can_be_changed_or_cleared()
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.HomeRosterId, 5, 1, "BB"));
        var cut = RenderGame(game.Id);

        Open(cut, "home", 5, 1);
        Assert.Equal("BB", Checked(cut, "home"));
        Pick(cut, "home", "HR");
        cut.Find("#home-at-bat-save").Click();
        cut.WaitForAssertion(() => Assert.Equal("HR", Cell(cut, "home-slot-5-inning-1-note")));
        Assert.Equal("HR", Assert.Single(await _grid.ListNotesAsync(game.Id)).Note);

        Open(cut, "home", 5, 1);
        Assert.Equal("HR", Checked(cut, "home"));
        cut.Find("#home-at-bat-clear").Click();
        cut.WaitForAssertion(() => Assert.Equal("", Cell(cut, "home-slot-5-inning-1-note")));
        WaitClosed(cut, "home");
        Assert.Empty(await _grid.ListNotesAsync(game.Id));
        Assert.Equal("", Cell(RenderGame(game.Id), "home-slot-5-inning-1-note"));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("close")]
    [InlineData("escape")]
    public async Task Cancelling_or_closing_leaves_the_saved_result_unchanged(string how)
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 1, 1, "K"));
        var cut = RenderGame(game.Id);

        Open(cut, "away", 1, 1);
        Pick(cut, "away", "1B");
        switch (how)
        {
            case "cancel": cut.Find("#away-at-bat-cancel").Click(); break;
            case "close": cut.Find("#away-at-bat-close").Click(); break;
            default: cut.Find("#away-at-bat-dialog").KeyDown(new KeyboardEventArgs { Key = "Escape" }); break;
        }

        WaitClosed(cut, "away");
        Assert.Equal("K", Cell(cut, "away-slot-1-inning-1-note"));
        Assert.Equal("K", Assert.Single(await _grid.ListNotesAsync(game.Id)).Note);
        Open(cut, "away", 1, 1);
        Assert.Equal("K", Checked(cut, "away"));
    }

    [Fact]
    public async Task Older_free_text_still_shows_opens_with_nothing_selected_and_is_replaced_on_save()
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 2, 3, "6-3"));
        var cut = RenderGame(game.Id);
        Assert.Equal("6-3", Cell(cut, "away-slot-2-inning-3-note"));

        Open(cut, "away", 2, 3);
        Assert.Null(Checked(cut, "away"));
        Assert.True(cut.Find("#away-at-bat-save").HasAttribute("disabled"));
        Pick(cut, "away", "GO");
        cut.Find("#away-at-bat-save").Click();

        cut.WaitForAssertion(() => Assert.Equal("GO", Cell(cut, "away-slot-2-inning-3-note")));
        Assert.Equal("GO", Assert.Single(await _grid.ListNotesAsync(game.Id)).Note);
    }

    [Fact]
    public async Task Opening_focuses_the_dialog_and_escape_returns_focus_to_the_cell()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        // bUnit writes a ref id into the markup only on the render that captured it, so read the cell's now.
        var cellRef = ElementRef(cut, "#away-slot-4-inning-6-note");
        Assert.False(string.IsNullOrEmpty(cellRef));
        Open(cut, "away", 4, 6);
        var dialogRef = ElementRef(cut, "#away-at-bat-dialog");
        cut.WaitForAssertion(() => Assert.Equal(dialogRef,
            ((ElementReference)JSInterop.VerifyFocusAsyncInvoke().Arguments[0]!).Id));

        cut.Find("#away-at-bat-dialog").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        WaitClosed(cut, "away");
        cut.WaitForAssertion(() => Assert.Equal(cellRef,
            ((ElementReference)JSInterop.VerifyFocusAsyncInvoke(calledTimes: 2)[1].Arguments[0]!).Id));
    }

    private static string? CheckedBases(IRenderedComponent<GamePage> cut, string side) =>
        cut.FindAll($"#{side}-at-bat-bases input[type=radio]").SingleOrDefault(r => r.HasAttribute("checked"))?.GetAttribute("value");

    private static void PickBases(IRenderedComponent<GamePage> cut, string side, int bases)
    {
        cut.FindAll($"#{side}-at-bat-bases input[type=radio]").Single(r => r.GetAttribute("value") == $"{bases}").Change($"{bases}");
        cut.WaitForAssertion(() => Assert.Equal($"{bases}", CheckedBases(cut, side)));
    }

    /// <summary>The diamond segments the cell draws, each as "x1,y1-x2,y2" in drawing order.</summary>
    private static string[] Segments(IRenderedComponent<GamePage> cut, string cellId) =>
        cut.FindAll($"#{cellId} svg.bases-diamond line").Select(l =>
            $"{l.GetAttribute("x1")},{l.GetAttribute("y1")}-{l.GetAttribute("x2")},{l.GetAttribute("y2")}").ToArray();

    // viewBox 0 0 20 20: bottom centre (10,18), centre right (18,10), top centre (10,2), centre left (2,10).
    private static readonly string[] Diamond = ["10,18-18,10", "18,10-10,2", "10,2-2,10", "2,10-10,18"];

    [Fact]
    public async Task Dialog_offers_bases_reached_none_to_home_with_none_picked_for_an_empty_cell()
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        Open(cut, "away", 1, 1);

        var radios = cut.FindAll("#away-at-bat-bases input[type=radio]");
        Assert.Equal(["0", "1", "2", "3", "4"], radios.Select(r => r.GetAttribute("value")));
        var text = cut.Find("#away-at-bat-bases").TextContent;
        foreach (var label in new[] { "Bases reached", "none", "1st", "2nd", "3rd", "home" })
        {
            Assert.Contains(label, text);
        }
        Assert.Equal("0", CheckedBases(cut, "away"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Saving_bases_reached_draws_that_many_segments_in_order_and_survives_a_reload(int bases)
    {
        var game = await NewGameAsync();
        var cut = RenderGame(game.Id);

        Open(cut, "home", 2, 3);
        Pick(cut, "home", "1B");
        PickBases(cut, "home", bases);
        cut.Find("#home-at-bat-save").Click();

        WaitClosed(cut, "home");
        cut.WaitForAssertion(() => Assert.Equal(Diamond[..bases], Segments(cut, "home-slot-2-inning-3-note")));
        Assert.Equal("1B", Cell(cut, "home-slot-2-inning-3-note"));
        var note = Assert.Single(await _grid.ListNotesAsync(game.Id));
        Assert.Equal(("1B", bases), (note.Note, note.BasesReached));

        var reloaded = RenderGame(game.Id);
        Assert.Equal(Diamond[..bases], Segments(reloaded, "home-slot-2-inning-3-note"));
        Assert.Empty(Segments(reloaded, "home-slot-2-inning-4-note"));
        Open(reloaded, "home", 2, 3);
        Assert.Equal($"{bases}", CheckedBases(reloaded, "home"));
    }

    [Fact]
    public async Task Clear_resets_bases_reached_to_none()
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 4, 5, "2B", 2));
        var cut = RenderGame(game.Id);
        Assert.Equal(Diamond[..2], Segments(cut, "away-slot-4-inning-5-note"));

        Open(cut, "away", 4, 5);
        Assert.Equal("2", CheckedBases(cut, "away"));
        cut.Find("#away-at-bat-clear").Click();

        WaitClosed(cut, "away");
        cut.WaitForAssertion(() => Assert.Empty(Segments(cut, "away-slot-4-inning-5-note")));
        Assert.Empty(await _grid.ListNotesAsync(game.Id));
        Open(cut, "away", 4, 5);
        Assert.Equal("0", CheckedBases(cut, "away"));
    }

    [Fact]
    public async Task Cells_with_no_bases_reached_draw_no_segments_and_older_notes_still_show()
    {
        var game = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 1, 1, "K"));
        Assert.Null(await _grid.SaveNoteAsync(game.Id, game.AwayRosterId, 2, 1, "6-3"));
        var cut = RenderGame(game.Id);

        Assert.Equal("K", Cell(cut, "away-slot-1-inning-1-note"));
        Assert.Equal("6-3", Cell(cut, "away-slot-2-inning-1-note"));
        Assert.Empty(cut.FindAll("#away-batting-grid svg.bases-diamond line"));
        Assert.Empty(cut.FindAll("#home-batting-grid svg.bases-diamond line"));
    }
}
