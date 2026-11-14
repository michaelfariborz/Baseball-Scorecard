using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.BattingGrids;

// Position changes in the batting grid of the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class PositionChangesFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;

    // Slot 1 bats the CF, slot 6 the RF.
    private static readonly Position[] Positions9 =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.ThirdBase,
        Position.RightField, Position.Catcher, Position.SecondBase, Position.Pitcher,
    ];

    public PositionChangesFormDbTests(PostgresFixture fixture)
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

    /// <summary>A 9-inning game; the away lineup bats players 1–9 in order, player 10 pinch hits for slot 6 in the 7th.</summary>
    private sealed record Setup(Game Game, List<Player> Away)
    {
        public Player PinchHitter => Away[9];
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
        for (var i = 1; i <= 11; i++)
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
        Assert.Null(await new SubstitutionService(f).AddAsync(game.Id, away.Id, 6, players[9].Id, SubstitutionKind.PinchHitter, 7));
        return new Setup(game, players);
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#away-slot-1-inning-1-note")));
        return cut;
    }

    private static void ChangePosition(IRenderedComponent<GamePage> cut, Player player, Position position, string inning)
    {
        cut.Find("#away-pos-player").Change($"{player.Id}");
        cut.Find("#away-pos-position").Change(position.ToString());
        cut.Find("#away-pos-inning").Change(inning);
        cut.Find("#away-pos-add").Click();
    }

    /// <summary>The Pos text of every player row of one batting slot, starter first.</summary>
    private static List<string> SlotPositions(IRenderedComponent<GamePage> cut, int slot) =>
        cut.FindAll($"#away-batting-grid tbody tr[data-slot='{slot}']")
            .Select(tr => tr.QuerySelectorAll(".player-cell")[2].TextContent.Trim())
            .ToList();

    [Fact]
    public async Task A_pinch_hitter_who_stays_in_at_CF_and_the_CF_who_moves_to_RF_show_both_positions()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        ChangePosition(cut, s.PinchHitter, Position.CenterField, "8");
        cut.WaitForAssertion(() => Assert.Equal(["RF", "PH, CF"], SlotPositions(cut, 6)));
        ChangePosition(cut, s.Away[0], Position.RightField, "8");
        cut.WaitForAssertion(() => Assert.Equal(["CF, RF"], SlotPositions(cut, 1)));

        Assert.Empty(cut.FindAll("#away-batting-grid-error"));
    }

    [Fact]
    public async Task Only_players_in_the_game_and_fielding_positions_can_be_picked()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        var playerIds = cut.FindAll("#away-pos-player option").Select(o => o.GetAttribute("value")).Where(v => v != "0");
        Assert.Equal(s.Away.Take(10).Select(p => $"{p.Id}").Order(), playerIds.Order());
        var positions = cut.FindAll("#away-pos-position option").Select(o => o.TextContent.Trim());
        Assert.DoesNotContain("PH", positions);
        Assert.DoesNotContain("PR", positions);
        Assert.Contains("DH", positions);
    }

    [Fact]
    public async Task A_refused_change_shows_its_message_and_changes_nothing()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        ChangePosition(cut, s.PinchHitter, Position.CenterField, "6");

        cut.WaitForAssertion(() => Assert.Equal(SubstitutionService.BeforeEnteredMessage(7),
            cut.Find("#away-batting-grid-error").TextContent.Trim()));
        Assert.Equal(["RF", "PH"], SlotPositions(cut, 6));

        // The starter the pinch hitter replaced has left the game by the 8th.
        ChangePosition(cut, s.Away[5], Position.CenterField, "8");
        cut.WaitForAssertion(() => Assert.Equal(SubstitutionService.NotInGameMessage,
            cut.Find("#away-batting-grid-error").TextContent.Trim()));
        Assert.Equal(["RF", "PH"], SlotPositions(cut, 6));
    }

    [Fact]
    public async Task Saved_changes_reload_and_removing_one_puts_the_player_back()
    {
        var s = await NewGameAsync();
        var f = new FixtureDbFactory(_fixture);
        var subs = new SubstitutionService(f);
        Assert.Null(await subs.AddPositionChangeAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id, Position.RightField, 8));

        var cut = RenderGame(s.Game.Id);
        Assert.Equal(["CF, RF"], SlotPositions(cut, 1));

        var change = (await subs.ListPositionChangesAsync(s.Game.Id, s.Game.AwayRosterId)).Single();
        cut.Find($"#away-pos-change-{change.Id}-remove").Click();

        cut.WaitForAssertion(() => Assert.Equal(["CF"], SlotPositions(cut, 1)));
        Assert.Empty(await subs.ListPositionChangesAsync(s.Game.Id, s.Game.AwayRosterId));
    }
}
