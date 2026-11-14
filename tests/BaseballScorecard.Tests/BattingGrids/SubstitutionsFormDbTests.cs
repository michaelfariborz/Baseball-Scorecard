using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.BattingGrids;

// Substitutes in the batting grid of the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class SubstitutionsFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;

    private static readonly Position[] Positions9 =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.RightField,
        Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.Pitcher,
    ];

    public SubstitutionsFormDbTests(PostgresFixture fixture)
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

    /// <summary>A 9-inning game; the away lineup bats players 1–9 in order, 10–12 sit on the bench. Home has no lineup.</summary>
    private sealed record Setup(Game Game, List<Player> Away)
    {
        public Player Bench(int i) => Away[8 + i];
    }

    private async Task<Setup> NewGameAsync()
    {
        var f = new FixtureDbFactory(_fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id };
        Assert.Null(await new GameService(f).CreateAsync(game));
        game.AwayRoster = away;
        game.HomeRoster = home;
        var players = new List<Player>();
        for (var i = 1; i <= 12; i++)
        {
            var p = new Player { RosterId = away.Id, UniformNumber = $"{i}", FirstName = "First", LastName = $"Cub{i}" };
            Assert.Null(await new PlayerService(f).SaveAsync(p));
            players.Add(p);
        }
        Assert.Null(await new LineupService(f).SaveAsync(new Lineup
        {
            GameId = game.Id,
            RosterId = away.Id,
            Slots = Positions9.Select((pos, i) => new LineupSlot { PlayerId = players[i].Id, Position = pos }).ToList(),
        }));
        return new Setup(game, players);
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#away-slot-1-inning-1-note")));
        return cut;
    }

    private static void AddSub(IRenderedComponent<GamePage> cut, int slot, Player player, string kind, string inning)
    {
        cut.Find("#away-sub-slot").Change($"{slot}");
        cut.Find("#away-sub-player").Change($"{player.Id}");
        cut.Find("#away-sub-kind").Change(kind);
        cut.Find("#away-sub-inning").Change(inning);
        cut.Find("#away-sub-add").Click();
    }

    /// <summary>The # / Name / Pos / Inn text of every player row of one batting slot, starter first.</summary>
    private static List<string[]> SlotRows(IRenderedComponent<GamePage> cut, int slot) =>
        cut.FindAll($"#away-batting-grid tbody tr[data-slot='{slot}']")
            .Select(tr => tr.QuerySelectorAll(".player-cell").Select(c => c.TextContent.Trim()).ToArray())
            .ToList();

    private static string DialogTitle(IRenderedComponent<GamePage> cut, int slot, int inning)
    {
        cut.Find($"#away-slot-{slot}-inning-{inning}-note").Click();
        var title = cut.WaitForElement("#away-at-bat-title").TextContent.Trim();
        cut.Find("#away-at-bat-cancel").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#away-at-bat-dialog")));
        return title;
    }

    [Fact]
    public async Task Grid_has_an_Inn_column_left_blank_for_starters()
    {
        var s = await NewGameAsync();

        var cut = RenderGame(s.Game.Id);

        Assert.Equal(["#", "Name", "Pos", "Inn", "1"], cut.FindAll("#away-batting-grid thead th").Take(5).Select(th => th.TextContent.Trim()));
        Assert.Equal([["5", "First Cub5", "RF", ""]], SlotRows(cut, 5));
    }

    [Fact]
    public async Task Adding_a_pinch_hitter_then_a_pinch_runner_lists_them_under_the_starter_and_moves_the_at_bats()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        AddSub(cut, 5, s.Bench(1), "PinchHitter", "7");
        cut.WaitForAssertion(() => Assert.Equal(2, SlotRows(cut, 5).Count));
        AddSub(cut, 5, s.Bench(2), "PinchRunner", "7");
        cut.WaitForAssertion(() => Assert.Equal(3, SlotRows(cut, 5).Count));

        Assert.Equal([["5", "First Cub5", "RF", ""], ["10", "First Cub10", "PH", "7"], ["11", "First Cub11", "PR", "7"]],
            SlotRows(cut, 5));
        // Still one at-bat cell per slot per inning.
        Assert.Single(cut.FindAll("#away-slot-5-inning-7-note"));
        Assert.Equal("First Cub5, inning 6", DialogTitle(cut, 5, 6));
        Assert.Equal("First Cub10, inning 7", DialogTitle(cut, 5, 7));
        Assert.Equal("First Cub11, inning 8", DialogTitle(cut, 5, 8));
        Assert.Equal("First Cub4, inning 8", DialogTitle(cut, 4, 8));
    }

    [Fact]
    public async Task A_defensive_sub_shows_the_position_of_the_player_he_replaced()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        AddSub(cut, 2, s.Bench(1), "Defensive", "6");

        cut.WaitForAssertion(() => Assert.Equal([["2", "First Cub2", "SS", ""], ["10", "First Cub10", "SS", "6"]], SlotRows(cut, 2)));
        Assert.Equal("First Cub10, inning 6", DialogTitle(cut, 2, 6));
    }

    [Fact]
    public async Task Players_already_in_the_game_cannot_be_picked_and_a_bad_inning_shows_a_message()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        var options = cut.FindAll("#away-sub-player option").Where(o => o.GetAttribute("value") != "0").ToList();
        Assert.All(options.Where(o => o.HasAttribute("disabled")), o => Assert.Contains(o.GetAttribute("value"), s.Away.Take(9).Select(p => $"{p.Id}")));
        Assert.Equal(3, options.Count(o => !o.HasAttribute("disabled")));

        AddSub(cut, 5, s.Bench(1), "PinchHitter", "10");

        cut.WaitForAssertion(() => Assert.Equal(SubstitutionService.InningMessage(9), cut.Find("#away-batting-grid-error").TextContent.Trim()));
        Assert.Single(SlotRows(cut, 5));

        AddSub(cut, 5, s.Bench(1), "PinchHitter", "7");
        cut.WaitForAssertion(() => Assert.Equal(2, SlotRows(cut, 5).Count));
        Assert.Empty(cut.FindAll("#away-batting-grid-error"));
        Assert.True(cut.FindAll("#away-sub-player option").Single(o => o.GetAttribute("value") == $"{s.Bench(1).Id}").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Saved_at_bats_and_substitutes_reload_and_removing_a_sub_gives_the_slot_back()
    {
        var s = await NewGameAsync();
        var f = new FixtureDbFactory(_fixture);
        Assert.Null(await new BattingGridService(f).SaveNoteAsync(s.Game.Id, s.Game.AwayRosterId, 5, 7, "1B", 1));
        Assert.Null(await new SubstitutionService(f).AddAsync(s.Game.Id, s.Game.AwayRosterId, 5, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));

        var cut = RenderGame(s.Game.Id);

        Assert.Equal(2, SlotRows(cut, 5).Count);
        Assert.Equal("1B", cut.Find("#away-slot-5-inning-7-note .at-bat-note").TextContent.Trim());
        Assert.Equal("First Cub10, inning 7", DialogTitle(cut, 5, 7));

        var sub = (await new SubstitutionService(f).ListAsync(s.Game.Id, s.Game.AwayRosterId)).Single();
        cut.Find($"#away-sub-{sub.Id}-remove").Click();

        cut.WaitForAssertion(() => Assert.Single(SlotRows(cut, 5)));
        Assert.Equal("1B", cut.Find("#away-slot-5-inning-7-note .at-bat-note").TextContent.Trim());
        Assert.Equal("First Cub5, inning 7", DialogTitle(cut, 5, 7));
    }
}
